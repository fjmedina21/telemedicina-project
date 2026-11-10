"""Isolated local validation of STAFF-INVITATION-EMAIL-SPEC-v1.
Uses cached postgres:17, ephemeral Docker data, generated synthetic credentials,
and loopback SMTP only. Never loads api/src/.env or uses the remote database.
"""
import base64
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import time
import urllib.request
import uuid

ROOT = Path.cwd()
OUT = ROOT / "api/src/Tools/validation/STAFF-EMAIL-2026-11-09-v1"
RESULT = {"database": "ephemeral-local-postgres17", "real_email_sent": False, "checks": {}}
name = "telemedicina-staff-email-" + uuid.uuid4().hex[:10]
container_started = False
api = None
api_log = None

def emit(message):
    print(message, flush=True)

def available(port):
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", port))

def step(label, args, env):
    emit("Running " + label)
    with (OUT / (label + ".log")).open("w") as log:
        completed = subprocess.run(args, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
    RESULT["checks"][label] = {"exit_code": completed.returncode}
    if completed.returncode:
        raise RuntimeError(label + " failed; inspect its synthetic-only log")
    emit(label + ": PASS")

def ready(process, port):
    for _ in range(120):
        if process.poll() is not None:
            raise RuntimeError("Local synthetic API stopped")
        try:
            with urllib.request.urlopen("http://127.0.0.1:" + str(port) + "/api/v1/health", timeout=1) as response:
                if response.status == 200:
                    return
        except OSError:
            pass
        time.sleep(0.25)
    raise RuntimeError("Local synthetic API readiness timeout")

def sql(query):
    completed = subprocess.run(["docker", "exec", name, "psql", "-X", "-A", "-t",
        "-v", "ON_ERROR_STOP=1", "-U", "synthetic", "-d", "staff_email_test", "-c", query],
        text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if completed.returncode:
        raise RuntimeError("Synthetic SQL failed: " + completed.stderr.strip())
    return completed.stdout.strip()

def state(account):
    return sql("SELECT state FROM telemedicina_m1.email_outbox WHERE user_id='" + account["id"] + "' AND kind='staff-activation'")

def wait_state(account, expected):
    for _ in range(80):
        if state(account) == expected:
            return
        time.sleep(0.1)
    raise RuntimeError("Synthetic outbox did not reach " + expected + "; actual=" + state(account))

def queue_checks(env):
    sys.path.insert(0, str(ROOT / "api/src/Telemedicina.Tests/Integration"))
    import second_milestone_e2e as http
    http.BASE = env["MILESTONE_TEST_URL"]
    status, body = http.request("/auth/login", "POST", {
        "email": env["Milestone__AdministratorEmail"], "password": env["Milestone__AdministratorPassword"]})
    if status != 200:
        raise RuntimeError("Synthetic administrator login failed")
    access = body["data"]["accessToken"]
    def invite():
        status, body = http.request("/admin/staff/invitations", "POST", {
            "email": "synthetic-queue-" + uuid.uuid4().hex + "@example.invalid", "role": "Support"}, access)
        if status != 201:
            raise RuntimeError("Synthetic queue invitation failed")
        return body["data"]
    try:
        account = invite()
        wait_state(account, "Captured")
        # Hold the same advisory lock as identity and the worker. Requeue this
        # already captured development-only fixture, then wait for its lease.
        locker = subprocess.Popen(["docker", "exec", "-i", name, "psql", "-X", "-A", "-t",
            "-v", "ON_ERROR_STOP=1", "-U", "synthetic", "-d", "staff_email_test"],
            text=True, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        try:
            locker.stdin.write("SELECT pg_advisory_lock(hashtextextended('mailuser:" + account["id"] + "',0));\nSELECT 'LOCKED';\n")
            locker.stdin.flush()
            while True:
                line = locker.stdout.readline()
                if not line:
                    raise RuntimeError("Could not take synthetic delivery lock")
                if line.strip() == "LOCKED":
                    break
            sql("UPDATE telemedicina_m1.email_outbox SET state='Queued',attempts=0,sent_at=NULL,available_at=now() WHERE user_id='" + account["id"] + "' AND kind='staff-activation'")
            wait_state(account, "Sending")
            # Simulate a committed revocation while the worker is waiting:
            # the worker must re-read state/lease instead of sending stale data.
            locker.stdin.write("UPDATE telemedicina_m1.staff_invitations SET used=true WHERE user_id='" + account["id"] + "';\n"
                "UPDATE telemedicina_m1.email_outbox SET state='Superseded',payload_cipher='',locked_until=NULL WHERE user_id='" + account["id"] + "' AND kind='staff-activation';\n"
                "SELECT pg_advisory_unlock(hashtextextended('mailuser:" + account["id"] + "',0));\n\\q\n")
            locker.stdin.flush()
            locker.stdin.close()
            if locker.wait(timeout=10):
                raise RuntimeError("Synthetic invalidation failed")
        finally:
            if locker.poll() is None:
                locker.terminate()
                locker.wait(timeout=5)
        time.sleep(2)
        if state(account) != "Superseded":
            raise RuntimeError("Worker overwrote a revoked leased message")
        RESULT["checks"]["leased_message_invalidated_before_send"] = {"status": "PASS", "invalidation": "controlled SQL under shared lock"}
        emit("leased_message_invalidated_before_send: PASS")

        account = invite()
        wait_state(account, "Captured")
        # Outbox expiry is deliberately future: the exact invitation expiry must win.
        sql("BEGIN; UPDATE telemedicina_m1.staff_invitations SET expires=0 WHERE user_id='" + account["id"] + "';"
            "UPDATE telemedicina_m1.email_outbox SET state='Queued',attempts=0,sent_at=NULL,available_at=now(),expires_at=now()+interval '15 minutes' WHERE user_id='" + account["id"] + "' AND kind='staff-activation'; COMMIT;")
        wait_state(account, "Expired")
        RESULT["checks"]["exact_invitation_expiry"] = {"status": "PASS"}
        emit("exact_invitation_expiry: PASS")

        ordinary = str(uuid.uuid4())
        sql("BEGIN; INSERT INTO telemedicina_m1.users(id,email,password_hash,role,verified,active) VALUES('"
            + ordinary + "','synthetic-inactive-" + ordinary + "@example.invalid','disabled-synthetic-login','Patient',true,false);"
            "INSERT INTO telemedicina_m1.email_outbox(id,event_key,user_id,recipient,kind,subject,payload_cipher,channel) VALUES('"
            + ordinary + "','synthetic-inactive:" + ordinary + "','" + ordinary + "','synthetic-inactive@example.invalid','auth:verify','synthetic','','development'); COMMIT;")
        for _ in range(80):
            current = sql("SELECT state FROM telemedicina_m1.email_outbox WHERE id='" + ordinary + "'")
            if current == "Superseded":
                break
            time.sleep(0.1)
        else:
            raise RuntimeError("Ordinary inactive account mail was not superseded")
        RESULT["checks"]["ordinary_inactive_mail_rejected"] = {"status": "PASS"}
        emit("ordinary_inactive_mail_rejected: PASS")
    finally:
        http.request("/auth/logout", "POST", {}, access)

def main():
    global container_started, api, api_log
    OUT.mkdir(parents=True, exist_ok=True)
    for port in (5094, 5096, 8025):
        available(port)
    env = {key: value for key, value in os.environ.items()
        if not key.startswith(("ConnectionStrings__", "Milestone__", "Mail__", "DemoReset__"))}
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        db_port = sock.getsockname()[1]
    password = secrets.token_urlsafe(32)
    env.update({
        "POSTGRES_PASSWORD": password, "POSTGRES_USER": "synthetic", "POSTGRES_DB": "staff_email_test",
        "ASPNETCORE_ENVIRONMENT": "Development", "Milestone__Enabled": "true",
        "Milestone__PortalOrigin": "http://localhost:3000",
        "Milestone__AdministratorEmail": "synthetic-admin-" + uuid.uuid4().hex + "@example.invalid",
        "Milestone__AdministratorPassword": secrets.token_urlsafe(32),
        "Milestone__AdmissionEmail": "synthetic-admission-" + uuid.uuid4().hex + "@example.invalid",
        "Milestone__AdmissionPassword": secrets.token_urlsafe(32),
        "Milestone__MailboxKey": secrets.token_hex(32),
        "Mail__Mode": "development",
        "Mail__PayloadKey": base64.b64encode(secrets.token_bytes(32)).decode(),
        "MILESTONE_TEST_SYNTHETIC": "1", "MILESTONE_CONFIGURATION": "Release",
        "MILESTONE_TEST_URL": "http://127.0.0.1:5096/api/v1",
        "ConnectionStrings__Telemedicina": "Host=127.0.0.1;Port=" + str(db_port)
            + ";Database=staff_email_test;Username=synthetic;Password=" + password
    })
    emit("Starting ephemeral PostgreSQL from cached postgres:17")
    result = subprocess.run(["docker", "run", "--rm", "-d", "--name", name,
        "--label", "telemedicina.validation=staff-email", "--tmpfs", "/var/lib/postgresql/data",
        "-p", "127.0.0.1:" + str(db_port) + ":5432",
        "-e", "POSTGRES_PASSWORD", "-e", "POSTGRES_USER", "-e", "POSTGRES_DB", "postgres:17"],
        env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        raise RuntimeError("Could not start cached PostgreSQL test image")
    container_started = True
    for _ in range(80):
        result = subprocess.run(["docker", "exec", name, "pg_isready", "-U", "synthetic", "-d", "staff_email_test"],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if result.returncode == 0:
            break
        time.sleep(0.25)
    else:
        raise RuntimeError("PostgreSQL readiness timeout")
    base = ["dotnet", "run", "--configuration", "Release", "--no-build", "--no-launch-profile",
        "--project", "api/src/Telemedicina.Api/Telemedicina.Api.csproj", "--"]
    for label, action in (("migrations", "--migrate"), ("seed_admission", "--seed-synthetic"),
                          ("seed_admin", "--seed-staff-synthetic")):
        step(label, base + [action], env)
    if "--skip-smtp" not in sys.argv:
        step("smtp_staff_invitations", [sys.executable,
            "api/src/Telemedicina.Tests/Integration/smtp_fixture_test.py", "--staff-invitations"], env)
    else:
        RESULT["checks"]["smtp_staff_invitations"] = {"status": "PASS_PREVIOUS_RUN", "log": "smtp_staff_invitations.log"}
    api_log = (OUT / "synthetic_api.log").open("w")
    api = subprocess.Popen(base + ["--urls", "http://127.0.0.1:5096"], cwd=ROOT, env=env,
        stdout=api_log, stderr=subprocess.STDOUT)
    ready(api, 5096)
    queue_checks(env)
    # Start the existing H02 suite with a fresh per-IP rate limiter.
    api.terminate()
    api.wait(timeout=10)
    api = subprocess.Popen(base + ["--urls", "http://127.0.0.1:5096"], cwd=ROOT, env=env,
        stdout=api_log, stderr=subprocess.STDOUT)
    ready(api, 5096)
    step("h02_regression", [sys.executable,
        "api/src/Telemedicina.Tests/Integration/second_milestone_e2e.py"], env)
    RESULT["status"] = "PASS"

try:
    main()
except Exception as error:
    RESULT["status"] = "FAIL"
    RESULT["error_type"] = type(error).__name__
    RESULT["error"] = str(error) if isinstance(error, RuntimeError) else type(error).__name__
    emit("Validation failed (" + type(error).__name__ + "); see synthetic logs.")
finally:
    if api is not None and api.poll() is None:
        api.terminate()
        try:
            api.wait(timeout=10)
        except subprocess.TimeoutExpired:
            api.kill()
            api.wait()
    if api_log is not None:
        api_log.close()
    if container_started:
        cleanup = subprocess.run(["docker", "stop", "--time", "2", name],
            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        RESULT["temporary_container_removed"] = cleanup.returncode == 0
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "integration-results.json").write_text(json.dumps(RESULT, indent=2) + "\n")
    emit(json.dumps(RESULT))
sys.exit(0 if RESULT.get("status") == "PASS" else 1)
