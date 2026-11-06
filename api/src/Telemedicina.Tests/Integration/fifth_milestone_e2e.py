"""H05 real HTTP contract/roles/ownership/workflow; synthetic loopback only."""
import json
import os
import secrets
import unittest
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone, timedelta
import fourth_milestone_e2e as h04
import second_milestone_e2e as h02

request=h02.request

@unittest.skipUnless(os.environ.get("MILESTONE_TEST_SYNTHETIC")=="1","Synthetic opt-in required")
class FifthMilestone(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if urllib.parse.urlparse(h02.BASE).hostname not in {"localhost","127.0.0.1","::1"} or os.environ.get("Mail__Mode")!="development":
            raise RuntimeError("Requires synthetic loopback development API")
        h04.FourthMilestone.setUpClass()
        cls.helper=h04.FourthMilestone.helper
        cls.actors=h04.FourthMilestone.actors
        try:
            email="synthetic-h05-http-"+uuid.uuid4().hex+"@example.invalid"
            password=secrets.token_hex(24)
            cls.helper.status(request("/auth/register","POST",{"email":email,"password":password}),202)
            cls.helper.status(request("/auth/verify","POST",{"email":email,"token":cls.helper.mailbox(email,"verify")}),200)
            cls.other=h02.SecondMilestone.login(email,password)
            cls.patient=cls.actors["Patient"]["accessToken"]
            cls.staff=cls.actors["Admission"]["accessToken"]
            cls.patient_id=cls.helper.status(request("/auth/me",token=cls.patient),200)["userId"]
            cls.other_id=cls.helper.status(request("/auth/me",token=cls.other["accessToken"]),200)["userId"]
            cls.service_body={"name":"Synthetic H05 HTTP","specialty":"Synthetic H05 HTTP","durationMinutes":30,
                "active":True,"isSynthetic":True,"approvalReference":None,"version":0}
            cls.service=cls.helper.status(request("/admin/catalog/services","POST",cls.service_body,cls.staff),201)["id"]
            cls.wrong_service=cls.helper.status(request("/admin/catalog/services","POST",cls.service_body,cls.staff),201)["id"]
            cls.practitioners=[]
            for _ in range(2):
                cls.practitioners.append(cls.helper.status(request("/admin/catalog/practitioners","POST",
                    {"name":"Synthetic H05 HTTP","active":True,"licenseRef":None,"serviceIds":[cls.service,cls.wrong_service],"version":0},cls.staff),201)["id"])
            cls.next_hour=0
            cls.original_slot=cls.slot()
            cls.body={"slotId":cls.original_slot["id"],"insurer":"Synthetic H05","memberLast4":"0000"}
            cls.helper.status(request("/appointments","POST",cls.body,cls.patient,{"Idempotency-Key":str(uuid.uuid4())}),400)
            cls.profile={"firstName":"Paciente","lastName":"Sintético","birthDate":"1990-01-01","phone":"8095550101",
                "address":"Dirección sintética","emergencyName":"Contacto sintético","emergencyPhone":"8095550102",
                "allergies":[],"medications":[],"conditions":[],"version":0}
            for token in (cls.patient,cls.other["accessToken"]):
                cls.helper.status(request("/patients/me","PATCH",cls.profile,token),200)
            cls.key="{"+str(uuid.uuid4()).upper()+"}"
            created=cls.helper.status(request("/appointments","POST",
                {**cls.body,"userId":cls.other_id,"status":"Confirmed"},cls.patient,{"Idempotency-Key":cls.key}),201)
            cls.appointment=created["id"]
            cls.path="/appointments/"+cls.appointment
        except Exception:
            h04.FourthMilestone.tearDownClass()
            raise

    @classmethod
    def tearDownClass(cls):
        h04.FourthMilestone.tearDownClass()

    @classmethod
    def slot(cls,service=None,practitioner=None):
        start=datetime.now(timezone.utc).replace(microsecond=0)+timedelta(days=10,hours=cls.next_hour)
        cls.next_hour+=1
        body={"serviceId":service or cls.service,"practitionerId":practitioner or cls.practitioners[0],"startAt":start.isoformat()}
        result=cls.helper.status(request("/admin/slots","POST",body,cls.staff),201)
        return {"id":result["id"],"end":(start+timedelta(minutes=30)).isoformat()}

    def status(self,response,expected,code=None):
        self.assertEqual(response[0],expected,"Unexpected H05 status; payload suppressed")
        if code is not None: self.assertEqual(response[1].get("code"),code,"Unexpected H05 safe code")
        return response[1].get("data")
    def detail(self):
        return self.status(request(self.path,token=self.patient),200)

    def test_01_role_and_horizontal_matrix(self):
        for role,session in self.actors.items():
            token=session["accessToken"]
            for suffix in ("","/history"):
                with self.subTest(role=role,operation="read",suffix=suffix):
                    self.status(request(self.path+suffix,token=token),200 if role in {"Patient","Admission"} else 403)
                    self.status(request(self.path+suffix),401)
            if role!="Patient":
                self.status(request(self.path+"/cancel","POST",{"version":1},token),403)
                self.status(request(self.path+"/reschedule","POST",{"slotId":self.original_slot["id"],"version":1},token),403)
                self.status(request("/appointments","POST",self.body,token,{"Idempotency-Key":str(uuid.uuid4())}),403)
            if role!="Admission":
                self.status(request("/admin/work-queue",token=token),403)
                self.status(request("/admin"+self.path+"/validation","PATCH",{},token),403)
        for suffix in ("","/history"):
            self.status(request(self.path+suffix,token=self.other["accessToken"]),404)
        self.status(request(self.path+"/cancel","POST",{"version":1},self.other["accessToken"]),404)
        self.status(request(self.path+"/reschedule","POST",{"slotId":self.original_slot["id"],"version":1},self.other["accessToken"]),404)
        detail=self.detail()
        self.assertEqual(detail["patientId"],self.patient_id)
        self.assertEqual(detail["status"],"PendingValidation")
        self.assertEqual(detail["coverageMethod"],"Manual")
        self.assertTrue(detail["coverageRequiresReview"])

    def test_02_literal_replay_after_offer_and_profile_change(self):
        self.status(request("/patients/me","PATCH",{**self.profile,"version":1,"address":"Otra dirección sintética"},self.patient),200)
        self.status(request("/admin/catalog/services/"+self.service,"PATCH",
            {**self.service_body,"active":False,"version":1},self.staff),200)
        replay=self.status(request("/appointments","POST",self.body,self.patient,{"Idempotency-Key":self.key}),200)
        self.assertEqual(replay["id"],self.appointment)
        self.status(request("/appointments","POST",{**self.body,"insurer":"different"},self.patient,{"Idempotency-Key":self.key}),409,"IDEMPOTENCY_MISMATCH")
        self.status(request("/admin/catalog/services/"+self.service,"PATCH",
            {**self.service_body,"active":True,"version":2},self.staff),200)
        history=self.status(request(self.path+"/history",token=self.patient),200)
        self.assertEqual(len(history),1)
        self.assertEqual(self.detail()["version"],1)

    def test_03_binding_filters_and_openapi(self):
        for path in ("/appointments/me","/admin/work-queue"):
            token=self.patient if path.startswith("/appointments") else self.staff
            for query in ("status=invalid","status=Unknown","coverageStatus=approved","from=2040-01-01T00%3A00%3A00Z",
                          "from=2040-01-01T00%3A00%3A00&to=2040-01-02T00%3A00%3A00Z"):
                self.status(request(path+"?"+query,token=token),400)
        self.status(request("/appointments","POST",{**self.body,"slotId":str(uuid.UUID(int=0))},self.patient,
            {"Idempotency-Key":str(uuid.uuid4())}),400,"VALIDATION_FAILED")
        self.status(request("/admin"+self.path+"/validation","PATCH",
            {"status":"Approved","evidence":"synthetic","validUntil":"2040-01-01T00:00:00","version":1},self.staff),400,"VALIDATION_FAILED")
        self.assertEqual(self.detail()["version"],1)
        origin=h02.BASE.split("/api/v1")[0]
        with urllib.request.urlopen(origin+"/openapi/v1.json",timeout=25) as response: doc=json.load(response)
        for path in ("/api/v1/appointments/me","/api/v1/appointments/{id}","/api/v1/appointments/{id}/history","/api/v1/admin/work-queue"):
            self.assertIn(path,doc["paths"])
        schemas=doc.get("components",{}).get("schemas",{})
        self.assertTrue(any("coverageValidityReason" in schema.get("properties",{}) for schema in schemas.values()))
        self.assertTrue(any(set(schema.get("properties",{}))=={"slotId","insurer","memberLast4"} for schema in schemas.values()))

    def test_04_review_reschedule_cancel_and_history(self):
        review={"status":"Approved","evidence":" synthetic favorable ","validUntil":self.original_slot["end"],"version":1}
        self.status(request("/admin"+self.path+"/validation","PATCH",review,self.staff),200)
        detail=self.detail()
        self.assertEqual((detail["status"],detail["coverageStatus"]),("Confirmed","Approved"))
        self.assertFalse(detail["coverageRequiresReview"])
        self.status(request(self.path+"/reschedule","POST",{"slotId":self.original_slot["id"],"version":2},self.patient),409,"SAME_SLOT")
        wrong=self.slot(service=self.wrong_service)
        self.status(request(self.path+"/reschedule","POST",{"slotId":wrong["id"],"version":2},self.patient),409,"SERVICE_CHANGE_NOT_ALLOWED")
        new=self.slot(practitioner=self.practitioners[1])
        self.status(request(self.path+"/reschedule","POST",{"slotId":new["id"],"version":2},self.patient),200)
        detail=self.detail()
        self.assertEqual(detail["practitionerId"],self.practitioners[1])
        self.assertEqual((detail["status"],detail["coverageStatus"],detail["version"]),("PendingValidation","ManualReview",3))
        for key in ("evidence","validUntil","reviewedBy","reviewedAt"): self.assertIsNone(detail[key])
        self.status(request(self.path+"/cancel","POST",{"version":3},self.patient),200)
        self.status(request(self.path+"/cancel","POST",{"version":3},self.patient),409,"INVALID_TRANSITION")
        history=self.status(request(self.path+"/history",token=self.patient),200)
        self.assertEqual([x["newStatus"] for x in history],["PendingValidation","Confirmed","PendingValidation","Cancelled"])
        self.assertEqual(self.detail()["version"],4)

    def test_05_revoked_session(self):
        session=h02.SecondMilestone.login(os.environ["Milestone__AdmissionEmail"],os.environ["Milestone__AdmissionPassword"])
        token=session["accessToken"]
        self.status(request("/auth/logout","POST",{},token),200)
        for path in (self.path,self.path+"/history",self.path+"/check-in-requirements","/admin/work-queue"):
            self.status(request(path,token=token),401)
        self.status(request("/appointments","POST",self.body,token,{
            "Idempotency-Key":str(uuid.uuid4())}),401)
        self.status(request(self.path+"/cancel","POST",{"version":1},token),401)
        self.status(request(self.path+"/reschedule","POST",
            {"slotId":self.original_slot["id"],"version":1},token),401)
        self.status(request("/admin"+self.path+"/validation","PATCH",{},token),401)

    def test_06_check_in_requirements_are_disabled_and_scoped(self):
        path=self.path+"/check-in-requirements"
        requirements=self.status(request(path,token=self.patient),200)
        self.assertEqual(requirements,{
            "status":"NotConfigured","canProceed":False,"configurationVersion":None})
        self.status(request(path,token=self.other["accessToken"]),404)
        self.status(request(path,token=self.staff),200)
        for role in ("Administrator","Practitioner","Support"):
            self.status(request(path,token=self.actors[role]["accessToken"]),403)
        self.status(request(path),401)

if __name__=="__main__": unittest.main()
