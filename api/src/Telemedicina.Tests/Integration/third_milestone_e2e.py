"""H03 localhost HTTP tests: synthetic accounts, no SMTP, no payload output.
Run API with Mail__Mode=development and MILESTONE_TEST_SYNTHETIC=1.
"""
import concurrent.futures
import json
import os
import unittest
import urllib.parse
import urllib.request
import uuid
from milestone_e2e import request, BASE

@unittest.skipUnless(os.environ.get("MILESTONE_TEST_SYNTHETIC")=="1", "Synthetic opt-in required")
class ThirdMilestone(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if urllib.parse.urlparse(BASE).hostname not in {"localhost","127.0.0.1","::1"} or os.environ.get("Mail__Mode")!="development":
            raise RuntimeError("H03 requires loopback and development mail")
        cls.tokens=[]
        for _ in range(2):
            email="synthetic-h03-"+uuid.uuid4().hex+"@example.invalid"; password=uuid.uuid4().hex+"!a9"
            if request("/auth/register","POST",{"email":email,"password":password})[0]!=202: raise RuntimeError("Synthetic registration failed")
            status,challenge=request("/auth/dev-mailbox?"+urllib.parse.urlencode({"email":email,"purpose":"verify"}),
                extra={"X-Dev-Mailbox-Key":os.environ["Milestone__MailboxKey"]})
            if status!=200: raise RuntimeError("Synthetic mailbox failed")
            if request("/auth/verify","POST",{"email":email,"token":challenge["token"]})[0]!=200: raise RuntimeError("Synthetic verification failed")
            status,session=request("/auth/login","POST",{"email":email,"password":password})
            if status!=200: raise RuntimeError("Synthetic login failed")
            cls.tokens.append(session["accessToken"])

    @classmethod
    def tearDownClass(cls):
        for token in cls.tokens: request("/auth/logout","POST",{},token)

    def status(self,response,expected):
        self.assertEqual(response[0],expected,"Unexpected HTTP status; payload suppressed")
        return response[1]

    def test_profile_journey_security_binding_and_openapi(self):
        token,other=self.tokens
        self.status(request("/patients/me/completeness"),401)
        missing=self.status(request("/patients/me/completeness",token=token),200)
        self.assertEqual(len(missing["missingFields"]),10)
        self.assertIsNone(self.status(request("/patients/me",token=other),200))
        profile={"firstName":" Paciente ","lastName":" Sintético ","birthDate":"1990-01-01","phone":"8095550101",
            "address":" Dirección sintética ","emergencyName":" Contacto ","emergencyPhone":"8095550102",
            "allergies":[" Dato, sintético "],"medications":[],"conditions":[],"version":0}
        self.status(request("/patients/me","PATCH",profile,token),200)
        data=self.status(request("/patients/me",token=token),200)
        self.assertEqual(data["profile"]["firstName"],"Paciente")
        self.assertEqual(data["profile"]["allergies"],["Dato, sintético"])
        self.assertEqual(data["completeness"]["status"],"Complete")
        before=data["declarationMetadata"]
        self.assertEqual(before["allergies"]["source"],"SelfDeclared")
        self.assertIsNotNone(before["allergies"]["updatedAt"])
        self.assertIsNone(before["conditions"]["updatedAt"])
        for field,value in [("patientId","foreign-synthetic"),("EMAIL","synthetic@example.invalid"),("declarationMetadata",{}),
                            ("firstName"," "),("phone","invalid"),("allergies",[" "]),("allergies",["x"]*21),
                            ("birthDate","not-a-date"),("version","wrong-type"),("allergies",[123])]:
            error=self.status(request("/patients/me","PATCH",{**profile,"version":1,field:value},token,raw=True),400)
            self.assertIn(error["code"],("INVALID_PROFILE","VALIDATION_FAILED"))
            self.assertTrue(error["errors"])
            self.assertTrue(all(isinstance(e,str) and ":" in e for e in error["errors"]))
            self.assertNotIn("foreign-synthetic",json.dumps(error))
        self.assertEqual(self.status(request("/patients/me",token=token),200)["version"],1)
        profile["version"]=1; profile["phone"]="8095550103"
        self.status(request("/patients/me","PATCH",profile,token),200)
        self.assertEqual(self.status(request("/patients/me",token=token),200)["declarationMetadata"],before)
        profile["version"]=2
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            statuses=list(pool.map(lambda _:request("/patients/me","PATCH",profile,token)[0],range(2)))
        self.assertEqual(sorted(statuses),[200,409])
        profile["version"]=3; profile["allergies"]=[]
        self.status(request("/patients/me","PATCH",profile,token),200)
        data=self.status(request("/patients/me",token=token),200)
        self.assertNotEqual(data["declarationMetadata"]["allergies"]["updatedAt"],before["allergies"]["updatedAt"])
        self.assertEqual(data["profile"]["allergies"],[])
        self.assertIsNone(self.status(request("/patients/me",token=other),200))
        origin=BASE.split("/api/v1")[0]
        with urllib.request.urlopen(origin+"/openapi/v1.json",timeout=20) as response: document=json.load(response)
        self.assertIn("/api/v1/patients/me/completeness",document["paths"])
        self.assertIn("200",document["paths"]["/api/v1/patients/me/completeness"]["get"]["responses"])
        schemas=document.get("components",{}).get("schemas",{})
        self.assertTrue(any("declarationMetadata" in value.get("properties",{}) for value in schemas.values()))
        self.assertTrue(any("missingFields" in value.get("properties",{}) for value in schemas.values()))
        if os.environ.get("Milestone__AdmissionEmail") and os.environ.get("Milestone__AdmissionPassword"):
            staff=self.status(request("/auth/login","POST",{"email":os.environ["Milestone__AdmissionEmail"],
                "password":os.environ["Milestone__AdmissionPassword"]}),200)
            try:
                self.status(request("/patients/me/completeness",token=staff["accessToken"]),403)
                self.status(request("/patients/me","PATCH",profile,staff["accessToken"]),403)
            finally: request("/auth/logout","POST",{},staff["accessToken"])

if __name__=="__main__": unittest.main()
