"""Explicit synthetic HTTP integration tests; does not migrate or delete database objects.
Run with MILESTONE_TEST_SYNTHETIC=1 against a local, development-only API.
The test requires a separately provisioned synthetic database and admission account.
"""
import concurrent.futures
import json
import os
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timezone, timedelta

BASE = os.environ.get("MILESTONE_TEST_URL", "http://127.0.0.1:5000/api/v1")
ALLOWED = {"localhost", "127.0.0.1", "::1"}
_auth_calls = []

def request(path, method="GET", body=None, token=None, extra=None, raw=False):
    # Keep synthetic journeys below the API's real auth limit; never disable protection.
    if path.startswith("/auth/"):
        while True:
            now = time.monotonic()
            _auth_calls[:] = [at for at in _auth_calls if now - at < 61]
            if len(_auth_calls) < 25:
                _auth_calls.append(now)
                break
            time.sleep(0.5)
    headers = {"Content-Type": "application/json", "X-Telemedicina-Client": "milestone"}
    if token:
        headers["Authorization"] = "Bearer " + token
    headers.update(extra or {})
    payload = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(BASE + path, data=payload, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=20) as response:
            parsed = json.load(response)
            return response.status, parsed if raw else parsed.get("data")
    except urllib.error.HTTPError as error:
        # Do not print bodies: credentials and patient data never belong in test output.
        return error.code, json.load(error) if raw else None

@unittest.skipUnless(os.environ.get("MILESTONE_TEST_SYNTHETIC") == "1",
    "Requires explicit synthetic test environment")
class MilestoneE2E(unittest.TestCase):
    def test_auth_response_models(self):
        email = "synthetic-contract-" + uuid.uuid4().hex + "@example.invalid"
        status, body = request("/auth/register", "POST", {"email": email, "password": uuid.uuid4().hex}, raw=True)
        self.assertEqual(status, 202)
        self.assertTrue(body["success"])
        self.assertEqual(body["status"], status)
        for payload, expected_code in [
            ({"email": email, "token": "!"}, "VALIDATION_FAILED"),
            ({"email": "unknown@example.invalid", "token": "ABC123"}, "INVALID_CHALLENGE")
        ]:
            status, body = request("/auth/verify", "POST", payload, raw=True)
            self.assertEqual(status, 400)
            self.assertFalse(body["success"])
            self.assertEqual(body["status"], status)
            self.assertEqual(body["code"], expected_code)
            self.assertTrue(body["traceId"])
            self.assertEqual(body["instance"], "/api/v1/auth/verify")
            self.assertIn("errors", body)

    def test_registration_code_scope_attempt_limit_and_single_use(self):
        mailbox = os.environ["Milestone__MailboxKey"]
        accounts = []
        for _ in range(2):
            email = "synthetic-code-" + uuid.uuid4().hex + "@example.invalid"
            self.assertEqual(request("/auth/register", "POST", {"email": email, "password": uuid.uuid4().hex})[0], 202)
            _, challenge = request("/auth/dev-mailbox?" + urllib.parse.urlencode({"email": email, "purpose": "verify"}),
                extra={"X-Dev-Mailbox-Key": mailbox})
            code = challenge["token"]
            self.assertRegex(code, r"^[A-Z0-9]{6}$")
            accounts.append((email, code))
        email, code = accounts[0]
        # A distinct account cannot consume this account's challenge.
        other_email, other_code = accounts[1]
        if code != other_code:
            self.assertEqual(request("/auth/verify", "POST", {"email": other_email, "token": code})[0], 400)
        self.assertEqual(request("/auth/verify", "POST", {"email": email.upper(), "token": code.lower()})[0], 200)
        self.assertEqual(request("/auth/verify", "POST", {"email": email, "token": code})[0], 400)
        wrong = ("A" if other_code[0] != "A" else "B") + other_code[1:]
        for _ in range(5):
            self.assertEqual(request("/auth/verify", "POST", {"email": other_email, "token": wrong})[0], 400)
        self.assertEqual(request("/auth/verify", "POST", {"email": other_email, "token": other_code})[0], 400)

    @classmethod
    def setUpClass(cls):
        if urllib.parse.urlparse(BASE).hostname not in ALLOWED:
            raise RuntimeError("Tests refuse non-loopback API URLs.")
        mailbox = os.environ["Milestone__MailboxKey"]
        cls.patients = []
        for _ in range(2):
            email = "synthetic-" + uuid.uuid4().hex + "@example.invalid"
            password = uuid.uuid4().hex + "!a9"
            status, _ = request("/auth/register", "POST", {"email": email, "password": password})
            assert status == 202
            query = urllib.parse.urlencode({"email": email, "purpose": "verify"})
            status, challenge = request("/auth/dev-mailbox?" + query, extra={"X-Dev-Mailbox-Key": mailbox})
            assert status == 200
            assert request("/auth/verify", "POST", {"email": email, "token": challenge["token"]})[0] == 200
            status, session = request("/auth/login", "POST", {"email": email, "password": password})
            assert status == 200
            profile = {"firstName": "Paciente", "lastName": "Sintético", "birthDate": "1990-01-01",
                "phone": "8095550101", "address": "Dirección de prueba",
                "emergencyName": "Contacto sintético", "emergencyPhone": "8095550102",
                "allergies": [], "medications": [], "conditions": [], "version": 0}
            assert request("/patients/me", "PATCH", profile, session["accessToken"])[0] == 200
            session["email"] = email
            session["password"] = password
            cls.patients.append(session)
        status, cls.staff = request("/auth/login", "POST", {
            "email": os.environ["Milestone__AdmissionEmail"],
            "password": os.environ["Milestone__AdmissionPassword"]})
        assert status == 200
        status, services = request("/services", token=cls.patients[0]["accessToken"])
        assert status == 200
        service_ids = {service["id"] for service in services}
        status, practitioners = request("/admin/catalog/practitioners", token=cls.staff["accessToken"])
        assert status == 200
        pair = next(((practitioner["id"], service_id)
                     for practitioner in practitioners if practitioner["active"]
                     for service_id in practitioner["serviceIds"] if service_id in service_ids), None)
        assert pair is not None, "Synthetic catalog requires an active assigned practitioner/service pair."
        cls.practitioner, cls.service = pair

    def new_slot(self):
        # Isolated future slot per test; avoids depending on seed availability.
        offset = 30 + (uuid.uuid4().int % 100000)
        start = datetime.now(timezone.utc).replace(second=0, microsecond=0) + timedelta(hours=offset)
        status, result = request("/admin/slots", "POST", {
            "serviceId": self.service, "practitionerId": self.practitioner,
            "startAt": start.isoformat()}, self.staff["accessToken"])
        self.assertEqual(status, 201)
        return result["id"], start

    def book(self, slot, patient=0, key=None):
        return request("/appointments", "POST",
            {"slotId": slot, "insurer": "Aseguradora sintética", "memberLast4": "0000"},
            self.patients[patient]["accessToken"], {"Idempotency-Key": key or str(uuid.uuid4())})

    def test_01_auth_and_horizontal_access(self):
        self.assertEqual(request("/patients/me")[0], 401)
        self.assertEqual(request("/admin/work-queue", token=self.patients[0]["accessToken"])[0], 403)
        slot, _ = self.new_slot()
        status, appointment = self.book(slot)
        self.assertEqual(status, 201)
        self.assertEqual(request("/appointments/" + appointment["id"], token=self.patients[1]["accessToken"])[0], 404)
        self.assertEqual(request("/appointments/" + str(uuid.uuid4()), token=self.staff["accessToken"])[0], 404)

    def test_02_idempotency_conflicts_and_database_read(self):
        slot, _ = self.new_slot()
        key = str(uuid.uuid4())
        first = self.book(slot, key=key)
        second = self.book(slot, key=key)
        self.assertEqual((first[0], second[0]), (201, 200))
        self.assertEqual(first[1]["id"], second[1]["id"])
        other, _ = self.new_slot()
        self.assertEqual(self.book(other, key=key)[0], 409)
        # A separately opened DB connection serves this GET; full process restart is a manual check.
        self.assertEqual(request("/appointments/" + first[1]["id"], token=self.patients[0]["accessToken"])[0], 200)

    def test_03_concurrent_booking(self):
        slot, _ = self.new_slot()
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            results = list(executor.map(lambda patient: self.book(slot, patient), [0, 1]))
        self.assertEqual(sorted(result[0] for result in results), [201, 409])

    def test_04_coverage_version_history_reschedule_and_cancel(self):
        slot, start = self.new_slot()
        status, appointment = self.book(slot)
        self.assertEqual(status, 201)
        body = {"status": "Approved", "evidence": "Referencia sintética",
                "validUntil": (start + timedelta(hours=2)).isoformat(), "version": 1}
        endpoint = "/admin/appointments/" + appointment["id"] + "/validation"
        self.assertEqual(request(endpoint, "PATCH", body, self.staff["accessToken"])[0], 200)
        self.assertEqual(request(endpoint, "PATCH", body, self.staff["accessToken"])[0], 409)
        detail = request("/appointments/" + appointment["id"], token=self.patients[0]["accessToken"])[1]
        self.assertEqual((detail["status"], detail["coverageStatus"]), ("Confirmed", "Approved"))
        new, _ = self.new_slot()
        self.assertEqual(request("/appointments/" + appointment["id"] + "/reschedule", "POST",
            {"slotId": new, "version": 2}, self.patients[0]["accessToken"])[0], 200)
        detail = request("/appointments/" + appointment["id"], token=self.patients[0]["accessToken"])[1]
        self.assertEqual((detail["status"], detail["coverageStatus"]), ("PendingValidation", "ManualReview"))
        self.assertEqual(request("/appointments/" + appointment["id"] + "/cancel", "POST",
            {"version": 3}, self.patients[0]["accessToken"])[0], 200)
        self.assertEqual(self.book(new, patient=1)[0], 201)
        history = request("/appointments/" + appointment["id"] + "/history",
            token=self.patients[0]["accessToken"])[1]
        self.assertEqual([h["newStatus"] for h in history],
            ["PendingValidation", "Confirmed", "PendingValidation", "Cancelled"])

    def test_05_blocked_slot_and_profile_concurrency(self):
        slot, _ = self.new_slot()
        self.assertEqual(request("/admin/slots/" + slot + "/block", "PATCH",
            {"blocked": True}, self.staff["accessToken"])[0], 200)
        self.assertEqual(self.book(slot)[0], 409)
        profile = request("/patients/me", token=self.patients[0]["accessToken"])[1]
        body = profile["profile"]; body["version"] = profile["version"]
        self.assertEqual(request("/patients/me", "PATCH", body, self.patients[0]["accessToken"])[0], 200)
        self.assertEqual(request("/patients/me", "PATCH", body, self.patients[0]["accessToken"])[0], 409)

    def test_06_refresh_rotation_and_revocation(self):
        original = dict(self.patients[0])
        status, rotated = request("/auth/refresh", "POST", {"refreshToken": original["refreshToken"]})
        self.assertEqual(status, 200)
        self.assertEqual(request("/auth/me", token=original["accessToken"])[0], 401)
        self.assertEqual(request("/auth/refresh", "POST", {"refreshToken": original["refreshToken"]})[0], 401)
        self.patients[0].update(rotated)
        self.assertEqual(request("/auth/me", token=rotated["accessToken"])[0], 200)

    def test_07_password_reset_revokes_existing_sessions(self):
        user = self.patients[1]
        self.assertEqual(request("/auth/password/forgot", "POST", {"email": user["email"]})[0], 202)
        query = urllib.parse.urlencode({"email": user["email"], "purpose": "reset"})
        status, challenge = request("/auth/dev-mailbox?" + query,
            extra={"X-Dev-Mailbox-Key": os.environ["Milestone__MailboxKey"]})
        self.assertEqual(status, 200)
        password = uuid.uuid4().hex + "!a9"
        self.assertEqual(request("/auth/password/reset", "POST",
            {"token": challenge["token"], "password": password})[0], 200)
        self.assertEqual(request("/auth/me", token=user["accessToken"])[0], 401)
        self.assertEqual(request("/auth/refresh", "POST", {"refreshToken": user["refreshToken"]})[0], 401)
        status, session = request("/auth/login", "POST", {"email": user["email"], "password": password})
        self.assertEqual(status, 200)
        user.update(session); user["password"] = password

    def test_08_staff_password_policy_development_only(self):
        # Approved development policy: staff signs in with its password.
        credentials = {"email": os.environ["Milestone__AdmissionEmail"],
            "password": os.environ["Milestone__AdmissionPassword"]}
        status, session = request("/auth/login", "POST", credentials)
        self.assertEqual(status, 200)
        self.assertEqual(request("/auth/me", token=session["accessToken"])[1]["role"], "Admission")
        self.assertEqual(request("/auth/logout", "POST", {}, session["accessToken"])[0], 200)
        credentials["password"] = uuid.uuid4().hex + uuid.uuid4().hex
        self.assertEqual(request("/auth/login", "POST", credentials)[0], 401)

    def test_09_persistence_across_actual_process_restart(self):
        from milestone_restart import check_restart
        check_restart()

    def test_10_catalog_membership_and_versions(self):
        suffix = uuid.uuid4().hex[:10]
        service = {"name": "Servicio sintético " + suffix, "specialty": "Pruebas",
            "durationMinutes": 30, "active": False, "isSynthetic": True, "approvalReference": "", "version": 0}
        status, saved = request("/admin/catalog/services", "POST", service, self.staff["accessToken"])
        self.assertEqual(status, 201)
        sid = saved["id"]
        self.assertEqual(request("/admin/catalog/services", token=self.patients[0]["accessToken"])[0], 403)
        public = request("/services", token=self.patients[0]["accessToken"])[1]
        self.assertNotIn(sid, [item["id"] for item in public])
        service.update({"active": True, "version": 1})
        self.assertEqual(request("/admin/catalog/services/" + sid, "PATCH", service, self.staff["accessToken"])[0], 200)
        self.assertEqual(request("/admin/catalog/services/" + sid, "PATCH", service, self.staff["accessToken"])[0], 409)
        person = {"name": "Profesional sintético " + suffix, "licenseRef": "SYNTHETIC",
            "active": True, "serviceIds": [sid], "version": 0}
        status, saved = request("/admin/catalog/practitioners", "POST", person, self.staff["accessToken"])
        self.assertEqual(status, 201)
        pid = saved["id"]
        start = datetime.now(timezone.utc).replace(second=0, microsecond=0) + timedelta(days=40, minutes=uuid.uuid4().int % 100000)
        status, slot = request("/admin/slots", "POST", {"serviceId": sid,
            "practitionerId": pid, "startAt": start.isoformat()}, self.staff["accessToken"])
        self.assertEqual(status, 201)
        person.update({"serviceIds": [], "version": 1})
        self.assertEqual(request("/admin/catalog/practitioners/" + pid, "PATCH", person, self.staff["accessToken"])[0], 200)
        self.assertEqual(request("/admin/slots", "POST", {"serviceId": sid,
            "practitionerId": pid, "startAt": (start + timedelta(hours=1)).isoformat()}, self.staff["accessToken"])[0], 400)
        self.assertEqual(self.book(slot["id"])[0], 409)
        service.update({"active": False, "version": 2})
        self.assertEqual(request("/admin/catalog/services/" + sid, "PATCH", service, self.staff["accessToken"])[0], 200)

    def test_11_notifications_preferences_and_deduplication(self):
        user = self.patients[0]["accessToken"]
        preferences = request("/notifications/preferences", token=user)[1]
        status, preferences = request("/notifications/preferences", "PATCH",
            {"emailEnabled": True, "version": preferences["version"]}, user)
        self.assertEqual(status, 200)
        slot, _ = self.new_slot()
        key = str(uuid.uuid4())
        status, appointment = self.book(slot, key=key)
        self.assertEqual(status, 201)
        self.assertEqual(self.book(slot, key=key)[0], 200)
        notices = request("/notifications/me", token=user)[1]
        matching = [item for item in notices if item["resourceId"] == appointment["id"]]
        self.assertEqual(len(matching), 1)
        notice = matching[0]
        self.assertNotIn("Aseguradora", notice["body"])
        self.assertEqual(request("/notifications/" + notice["id"] + "/read", "POST", {},
            self.patients[1]["accessToken"])[0], 404)
        self.assertEqual(request("/notifications/" + notice["id"] + "/read", "POST", {}, user)[0], 200)
        self.assertEqual(request("/appointments/" + appointment["id"] + "/cancel", "POST", {"version": 1}, user)[0], 200)
        status, result = request("/notifications/preferences", "PATCH",
            {"emailEnabled": False, "version": preferences["version"]}, user)
        self.assertEqual(status, 200)
        self.assertFalse(result["emailEnabled"])
        delivery = request("/admin/email-delivery", token=self.staff["accessToken"])[1]
        self.assertTrue(all("recipient" not in item and "payload_cipher" not in item for item in delivery))
        self.assertFalse(any(item["resourceId"] == appointment["id"] and item["kind"] == "requested" and
            item["state"] == "Queued" for item in delivery))

    def test_99_logout_revokes_access_and_refresh(self):
        for patient in self.patients:
            self.assertEqual(request("/auth/logout", "POST", {}, patient["accessToken"])[0], 200)
            self.assertEqual(request("/auth/me", token=patient["accessToken"])[0], 401)
            self.assertEqual(request("/auth/refresh", "POST", {"refreshToken": patient["refreshToken"]})[0], 401)

if __name__ == "__main__":
    unittest.main()
