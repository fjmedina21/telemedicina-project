"""Loopback-only SMTP integration; no real email or credentials are used."""
import asyncore
from email import policy
from email.parser import BytesParser
import os
from pathlib import Path
import re
import smtpd
import subprocess
import sys
import threading
import time
import urllib.parse
import uuid

def check_smtp():
    assert os.environ.get("MILESTONE_TEST_SYNTHETIC")=="1"
    staff_test = "--staff-invitations" in sys.argv
    if staff_test and not all(os.environ.get(key) for key in
            ("Milestone__AdministratorEmail", "Milestone__AdministratorPassword")):
        raise RuntimeError("Staff SMTP checks require a separately seeded synthetic administrator.")
    from milestone_e2e import request
    import milestone_e2e as api
    original=api.BASE
    api.BASE="http://127.0.0.1:5094/api/v1"
    accepted=[]
    class Receiver(smtpd.SMTPServer):
        calls=0
        def process_message(self,peer,mailfrom,rcpttos,data,**kwargs):
            self.calls+=1
            if self.calls==1:
                return "451 synthetic temporary failure"
            accepted.append((rcpttos,data))
    receiver=Receiver(("127.0.0.1",8025),None,decode_data=False)
    running=True
    def loop():
        while running:
            asyncore.loop(timeout=0.1,count=1)
    thread=threading.Thread(target=loop,daemon=True);thread.start()
    root=Path(__file__).resolve().parents[4]
    env=os.environ.copy()
    env.update({"Mail__Mode":"smtp","Mail__Host":"127.0.0.1","Mail__Port":"8025",
        "Mail__AllowLocalTest":"true","Mail__Username":"","Mail__Password":"",
        "Mail__Sender":"sender@example.invalid","Mail__RetrySeconds":"1"})
    configuration=os.environ.get("MILESTONE_CONFIGURATION","Debug")
    server=subprocess.Popen(["dotnet","run","--no-build","--no-launch-profile","--configuration",configuration,
        "--project",str(root/"api/src/Telemedicina.Api/Telemedicina.Api.csproj"),"--","--urls","http://127.0.0.1:5094"],
        cwd=root,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    try:
        for _ in range(80):
            try:
                if request("/health")[0]==200: break
            except OSError: pass
            if server.poll() is not None: raise RuntimeError("Synthetic SMTP API stopped.")
            time.sleep(0.25)
        else: raise RuntimeError("Synthetic SMTP API readiness timed out.")
        email="synthetic-smtp-"+uuid.uuid4().hex+"@example.invalid"
        password=uuid.uuid4().hex+"!a9"
        assert request("/auth/register","POST",{"email":email,"password":password})[0]==202
        assert request("/auth/dev-mailbox?"+urllib.parse.urlencode({"email":email,"purpose":"verify"}),
            extra={"X-Dev-Mailbox-Key":env["Milestone__MailboxKey"]})[0]==404
        for _ in range(160):
            if accepted: break
            time.sleep(0.25)
        else: raise RuntimeError("SMTP retry did not deliver the synthetic message.")
        assert receiver.calls==2 and len(accepted)==1
        message=BytesParser(policy=policy.default).parsebytes(accepted[0][1])
        body=message.get_content()
        tokens=re.findall(r"es: ([A-Z0-9]{6})\b",body)
        assert len(tokens)==1
        assert request("/auth/verify","POST",{"email":email,"token":tokens[0]})[0]==200
        assert request("/auth/verify","POST",{"email":email,"token":tokens[0]})[0]==400
        # Duplicate registration keeps a generic response and does not enqueue another verification.
        assert request("/auth/register","POST",{"email":email,"password":password})[0]==202
        time.sleep(2)
        assert len(accepted)==1
        if staff_test:
            status,admin = request("/auth/login","POST",{
                "email":env["Milestone__AdministratorEmail"],
                "password":env["Milestone__AdministratorPassword"]})
            assert status==200
            staff_session=None
            try:
                staff_email="synthetic-smtp-staff-"+uuid.uuid4().hex+"@example.invalid"
                status,account=request("/admin/staff/invitations","POST",
                    {"email":staff_email,"role":"Support"},admin["accessToken"])
                assert status==201
                assert set(account)=={"id","email","role","status","version"}
                assert account["status"]=="PendingActivation"
                assert request("/auth/dev-mailbox?"+urllib.parse.urlencode(
                    {"email":staff_email,"purpose":"staff-activation"}),
                    extra={"X-Dev-Mailbox-Key":env["Milestone__MailboxKey"]})[0]==404
                assert request("/admin/staff/"+account["id"]+"/invitation","POST",
                    {"expectedVersion":account["version"]},admin["accessToken"])[0]==429
                for _ in range(160):
                    staff_messages=[data for recipients,data in accepted if staff_email in recipients]
                    if staff_messages: break
                    time.sleep(0.25)
                else: raise RuntimeError("Pending staff SMTP invitation was not delivered.")
                assert len(staff_messages)==1
                message=BytesParser(policy=policy.default).parsebytes(staff_messages[0])
                body=message.get_content()
                tokens=re.findall(r"\b[A-F0-9]{64}\b",body)
                assert len(tokens)==1
                links=re.findall(r"https?://[^\s]+",body)
                assert len(links)==1
                link=urllib.parse.urlparse(links[0])
                assert link.path=="/activar-personal" and not link.query and not link.fragment
                assert tokens[0] not in links[0]
                password=uuid.uuid4().hex+"!a9"
                status,activation=request("/auth/staff/activate","POST",
                    {"token":tokens[0],"password":password})
                assert status==200 and activation=={"activated":True}
                assert request("/auth/staff/activate","POST",
                    {"token":tokens[0],"password":password})[0]==400
                status,staff_session=request("/auth/login","POST",{"email":staff_email,"password":password})
                assert status==200
                assert request("/admin/staff/invitations","POST",
                    {"email":"synthetic-forbidden-"+uuid.uuid4().hex+"@example.invalid","role":"Support"},
                    staff_session["accessToken"])[0]==403
            finally:
                if staff_session is not None:
                    request("/auth/logout","POST",{},staff_session["accessToken"])
                request("/auth/logout","POST",{},admin["accessToken"])
    finally:
        server.terminate()
        try: server.wait(timeout=10)
        except subprocess.TimeoutExpired: server.kill();server.wait()
        running=False;thread.join(timeout=2);receiver.close();api.BASE=original

if __name__=="__main__":
    check_smtp()
    print("SMTP loopback retry, deduplication, private verification and single-use token: PASS")
    if "--staff-invitations" in sys.argv:
        print("Staff SMTP delivery, pending activation, mailbox privacy, cooldown and permissions: PASS")
