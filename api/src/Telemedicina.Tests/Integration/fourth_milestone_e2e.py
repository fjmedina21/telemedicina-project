"""H04 HTTP authorization: synthetic actors, loopback and development mail only."""
import os
import secrets
import unittest
import urllib.parse
import uuid
import second_milestone_e2e as h02
BASE, request = h02.BASE, h02.request

@unittest.skipUnless(os.environ.get("MILESTONE_TEST_SYNTHETIC")=="1","Synthetic opt-in required")
class FourthMilestone(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if urllib.parse.urlparse(BASE).hostname not in {"localhost","127.0.0.1","::1"} or os.environ.get("Mail__Mode")!="development":
            raise RuntimeError("Requires loopback and development mail")
        h02.SecondMilestone.setUpClass()
        cls.helper=h02.SecondMilestone()
        try:
            cls.actors={"Administrator":h02.SecondMilestone.admin,"Admission":h02.SecondMilestone.admission}
            for role in ("Support","Practitioner"):
                cls.actors[role]=cls.helper.active_staff(role)[2]
            email="synthetic-h04-http-"+uuid.uuid4().hex+"@example.invalid"
            password=secrets.token_hex(24)
            cls.helper.status(request("/auth/register","POST",{"email":email,"password":password}),202)
            cls.helper.status(request("/auth/verify","POST",{"email":email,"token":cls.helper.mailbox(email,"verify")}),200)
            cls.actors["Patient"]=h02.SecondMilestone.login(email,password)
            for role,session in cls.actors.items():
                if cls.helper.status(request("/auth/me",token=session["accessToken"]),200)["role"]!=role:
                    raise AssertionError("Synthetic role mismatch")
        except Exception:
            h02.SecondMilestone.tearDownClass()
            raise
    @classmethod
    def tearDownClass(cls):
        h02.SecondMilestone.tearDownClass()
    def public(self):
        return ["/specialties","/services","/practitioners","/practitioners?serviceId="+str(uuid.UUID(int=1)),"/availability?serviceId="+str(uuid.UUID(int=1))]
    def admin(self):
        return ["/admin/catalog/services","/admin/catalog/practitioners","/admin/slots"]
    def writes(self):
        uid=str(uuid.UUID(int=1))
        return [("POST","/admin/catalog/services"),("PATCH","/admin/catalog/services/"+uid),("POST","/admin/catalog/practitioners"),("PATCH","/admin/catalog/practitioners/"+uid),("POST","/admin/slots"),("PATCH","/admin/slots/"+uid+"/block")]
    def check(self,path,expected,token=None,method="GET"):
        self.assertEqual(request(path,method,None if method=="GET" else {},token)[0],expected,"Unexpected H04 HTTP status; payload suppressed")
    def test_01_roles(self):
        for role,session in self.actors.items():
            token=session["accessToken"]
            for path in self.public():
                with self.subTest(role=role,path=path):
                    self.check(path,200 if role in {"Patient","Admission"} else 403,token)
            for path in self.admin():
                with self.subTest(role=role,path=path):
                    self.check(path,200 if role=="Admission" else 403,token)
            if role!="Admission":
                for method,path in self.writes():
                    with self.subTest(role=role,path=path,method=method):
                        self.check(path,403,token,method)
    def test_02_no_session(self):
        for path in self.public()+self.admin(): self.check(path,401)
        for method,path in self.writes(): self.check(path,401,method=method)
    def test_03_revoked(self):
        session=h02.SecondMilestone.login(os.environ["Milestone__AdmissionEmail"],os.environ["Milestone__AdmissionPassword"])
        token=session["accessToken"]
        self.check("/auth/logout",200,token,"POST")
        for path in self.public()+self.admin(): self.check(path,401,token)
        for method,path in self.writes(): self.check(path,401,token,method)
if __name__=="__main__": unittest.main()
