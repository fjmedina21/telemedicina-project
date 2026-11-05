"""Reproducible synthetic API, SMTP and Playwright checks; credentials stay in child environments."""
import argparse
import os
from pathlib import Path
import secrets
import signal
import socket
import subprocess
import sys
import time
import urllib.request

ROOT=Path(__file__).resolve().parents[4]
PROJECT=ROOT/"api/src/Telemedicina.Api/Telemedicina.Api.csproj"

def wait_ready(process,url):
    for _ in range(120):
        if process.poll() is not None:
            raise RuntimeError("Test service stopped before readiness.")
        try:
            with urllib.request.urlopen(url,timeout=2) as response:
                if response.status==200: return
        except OSError: pass
        time.sleep(0.25)
    raise RuntimeError("Test service readiness timed out.")

def stop(process):
    if process is None: return
    os.killpg(process.pid,signal.SIGTERM)
    try: process.wait(timeout=10)
    except subprocess.TimeoutExpired: os.killpg(process.pid,signal.SIGKILL);process.wait()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--configuration",choices=["Debug","Release"],default="Debug")
    parser.add_argument("--skip-browser",action="store_true")
    args=parser.parse_args()
    if not os.environ.get("ConnectionStrings__Telemedicina"):
        raise RuntimeError("Configure the private connection to a synthetic test database.")
    for port in [5091,5092,5094,8025]+([] if args.skip_browser else [3000]):
        try: connection=socket.create_connection(("127.0.0.1",port),timeout=0.3)
        except OSError: continue
        connection.close();raise RuntimeError("Test port is already occupied: "+str(port))
    env=os.environ.copy()
    for name in list(env):
        if name.startswith("Mail__"): del env[name]
    env.update({"ASPNETCORE_ENVIRONMENT":"Development","Milestone__Enabled":"true",
        "Milestone__MailboxKey":secrets.token_hex(32),
        "Milestone__AdmissionEmail":"synthetic-ci-"+secrets.token_hex(8)+"@example.invalid",
        "Milestone__AdmissionPassword":secrets.token_urlsafe(32),
        "Mail__Mode":"development","Mail__PayloadKey":base64.b64encode(secrets.token_bytes(32)).decode(),
        "MILESTONE_TEST_SYNTHETIC":"1","MILESTONE_TEST_URL":"http://127.0.0.1:5091/api/v1",
        "MILESTONE_CONFIGURATION":args.configuration,"MILESTONE_PORTAL_TEST_URL":"http://localhost:3000",
        "API_INTERNAL_URL":"http://127.0.0.1:5091/api/v1"})
    base=["dotnet","run","--no-build","--no-launch-profile","--configuration",args.configuration,"--project",str(PROJECT)]
    for action in ["--migrate","--seed-synthetic"]:
        subprocess.run(base+["--",action],cwd=ROOT,env=env,check=True)
    api=None;portal=None
    try:
        api=subprocess.Popen(base+["--","--urls","http://127.0.0.1:5091"],cwd=ROOT,env=env,
            stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
        wait_ready(api,"http://127.0.0.1:5091/api/v1/health")
        subprocess.run([sys.executable,str(ROOT/"api/src/Telemedicina.Tests/Integration/milestone_e2e.py")],
            cwd=ROOT,env=env,check=True)
        subprocess.run([sys.executable,str(ROOT/"api/src/Telemedicina.Tests/Integration/smtp_fixture_test.py")],
            cwd=ROOT,env=env,check=True)
        if not args.skip_browser:
            # Browser journeys get a fresh rate-limit window, with unchanged real protection.
            # Sessions and challenge attempts remain persisted in the same synthetic database.
            stop(api);api=None
            api=subprocess.Popen(base+["--","--urls","http://127.0.0.1:5091"],cwd=ROOT,env=env,
                stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
            wait_ready(api,"http://127.0.0.1:5091/api/v1/health")
            portal=subprocess.Popen(["npm","run","dev"],cwd=ROOT/"portalweb/src",env=env,
                stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
            wait_ready(portal,"http://localhost:3000/login")
            subprocess.run(["npm","run","test:e2e"],cwd=ROOT/"portalweb/src",env=env,check=True)
        print("Synthetic milestone checks: PASS")
    finally:
        stop(portal);stop(api)

if __name__=="__main__":
    main()
