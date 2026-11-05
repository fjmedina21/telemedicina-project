"""H02-SPEC-v1: synthetic localhost HTTP tests. Does not migrate, deploy or delete.
Private env: MILESTONE_TEST_SYNTHETIC=1, Mail__Mode=development,
Milestone__AdministratorEmail/Password, Milestone__AdmissionEmail/Password,
Milestone__MailboxKey. Seed/migrate explicitly before running.
Never enable HTTP tracing or print requests/responses from this suite.
"""
import concurrent.futures
import json
import os
import secrets
import threading
import time
import unittest
import urllib.error
import urllib.parse
import urllib.request
import uuid

BASE = os.environ.get("MILESTONE_TEST_URL", "http://127.0.0.1:5000/api/v1")
_calls = []
_gate = threading.Lock()


def request(path, method="GET", body=None, token=None, extra=None):
    # Preserve the real shared IP auth limiter (30/min), including staff invitations.
    limited = path.startswith("/auth/") or path.endswith("/invitation") or path.endswith("/invitations")
    if limited:
        while True:
            with _gate:
                now = time.monotonic()
                _calls[:] = [at for at in _calls if now - at < 61]
                if len(_calls) < 24:
                    _calls.append(now)
                    break
            time.sleep(0.2)
    headers = {"Content-Type": "application/json", "X-Telemedicina-Client": "milestone"}
    if token:
        headers["Authorization"] = "Bearer " + token
    headers.update(extra or {})
    req = urllib.request.Request(BASE + path, data=None if body is None else json.dumps(body).encode(),
                                 headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=25) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        try:
            return error.code, json.load(error)
        except (ValueError, UnicodeDecodeError):
            return error.code, {}


@unittest.skipUnless(os.environ.get("MILESTONE_TEST_SYNTHETIC") == "1", "Synthetic opt-in required")
class SecondMilestone(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if urllib.parse.urlparse(BASE).hostname not in {"localhost", "127.0.0.1", "::1"}:
            raise RuntimeError("Only loopback APIs are allowed.")
        if os.environ.get("Mail__Mode") != "development":
            raise RuntimeError("Requires private development mail mode; real SMTP tests are excluded.")
        for key in ("Milestone__AdministratorEmail", "Milestone__AdministratorPassword",
                    "Milestone__AdmissionEmail", "Milestone__AdmissionPassword", "Milestone__MailboxKey"):
            if not os.environ.get(key):
                raise RuntimeError("Missing private environment configuration: " + key)
        cls.sessions = []
        cls.admin = cls.login(os.environ["Milestone__AdministratorEmail"],
                              os.environ["Milestone__AdministratorPassword"])
        cls.admission = cls.login(os.environ["Milestone__AdmissionEmail"],
                                  os.environ["Milestone__AdmissionPassword"])

    @classmethod
    def login(cls, email, password, expected=200):
        status, body = request("/auth/login", "POST", {"email": email, "password": password})
        if status != expected:
            raise AssertionError("Unexpected synthetic login status.")
        if status != 200:
            return None
        session = body["data"]
        cls.sessions.append(session)
        return session

    @classmethod
    def tearDownClass(cls):
        for session in cls.sessions:
            request("/auth/logout", "POST", {}, session["accessToken"])

    def status(self, response, expected):
        self.assertEqual(response[0], expected, "HTTP status mismatch; payload intentionally omitted")
        return response[1].get("data")

    def mailbox(self, email, purpose="staff-activation"):
        query = urllib.parse.urlencode({"email": email, "purpose": purpose})
        body = self.status(request("/auth/dev-mailbox?" + query,
            extra={"X-Dev-Mailbox-Key": os.environ["Milestone__MailboxKey"]}), 200)
        return body["token"]

    def invite(self, role="Support"):
        email = "synthetic-h02-" + uuid.uuid4().hex + "@example.invalid"
        account = self.status(request("/admin/staff/invitations", "POST", {"email": email, "role": role},
                                      self.admin["accessToken"]), 201)
        self.assertTrue(set(account) == {"id", "email", "role", "status", "version"},
                        "Account response must contain only approved fields")
        return account, self.mailbox(email)

    def active_staff(self, role="Support"):
        account, token = self.invite(role)
        password = secrets.token_hex(24)
        self.status(request("/auth/staff/activate", "POST", {"token": token, "password": password}), 200)
        session = self.login(account["email"], password)
        account["version"] += 1
        account["status"] = "Active"
        return account, password, session

    def mutate(self, account, suffix, payload=None, method="POST", expected=200):
        body = {"expectedVersion": account["version"], **(payload or {})}
        result = request("/admin/staff/" + account["id"] + "/" + suffix, method, body, self.admin["accessToken"])
        data = self.status(result, expected)
        if expected == 200:
            account.update(data)
        return result

    def test_01_matrix_and_no_clinical_access(self):
        for role in ("Support", "Practitioner"):
            account, _, session = self.active_staff(role)
            me = self.status(request("/auth/me", token=session["accessToken"]), 200)
            self.assertEqual(me["role"], role)
            self.assertEqual(me["permissions"], ["identity.self"])
            for path in ("/patients/me", "/services", "/specialties", "/practitioners", "/availability",
                         "/appointments/me", "/appointments/" + str(uuid.uuid4()),
                         "/admin/work-queue", "/admin/audit-events", "/admin/staff",
                         "/admin/staff/audit-events"):
                self.status(request(path, token=session["accessToken"]), 403)
            self.status(request("/admin/staff/" + account["id"] + "/role", "PATCH",
                {"role": "Administrator", "expectedVersion": account["version"]}, session["accessToken"]), 403)
            # Unimplemented clinical paths must not become readable.
            for path in ("/encounters/" + str(uuid.uuid4()), "/documents/" + str(uuid.uuid4()) + "/download"):
                status, _ = request(path, token=session["accessToken"])
                self.assertTrue(status in (403, 404), "Clinical access must remain unavailable")
        self.status(request("/admin/work-queue", token=self.admin["accessToken"]), 403)
        self.status(request("/services", token=self.admin["accessToken"]), 403)
        self.status(request("/admin/staff", token=self.admission["accessToken"]), 403)
        self.status(request("/admin/work-queue", token=self.admission["accessToken"]), 200)

    def test_02_invitation_single_use_and_pending_transition(self):
        account, token = self.invite()
        self.mutate(account, "active", {"active": True}, "PATCH", expected=409)
        self.status(request("/auth/dev-mailbox?" + urllib.parse.urlencode(
            {"email": account["email"], "purpose": "staff-activation"})), 404)
        invalid = "F" * 64 if token != "F" * 64 else "A" * 64
        password = secrets.token_hex(24)
        self.status(request("/auth/staff/activate", "POST", {"token": invalid, "password": password}), 400)
        self.status(request("/auth/staff/activate", "POST", {"token": token, "password": password}), 200)
        self.status(request("/auth/staff/activate", "POST", {"token": token, "password": password}), 400)

    def test_03_suspend_reactivate_and_old_tokens(self):
        account, password, session = self.active_staff()
        original_version = account["version"]
        self.status(request("/admin/staff/" + account["id"] + "/active", "PATCH",
            {"expectedVersion": original_version}, self.admin["accessToken"]), 400)
        unchanged = self.status(request("/auth/me", token=session["accessToken"]), 200)
        self.assertEqual(unchanged["accountVersion"], original_version)
        self.mutate(account, "active", {"active": False}, "PATCH")
        self.status(request("/auth/me", token=session["accessToken"]), 401)
        self.status(request("/auth/refresh", "POST", {"refreshToken": session["refreshToken"]}), 401)
        self.login(account["email"], password, expected=401)
        self.mutate(account, "active", {"active": True}, "PATCH")
        self.status(request("/auth/me", token=session["accessToken"]), 401)
        fresh = self.login(account["email"], password)
        self.status(request("/auth/me", token=fresh["accessToken"]), 200)

    def test_04_role_change_revokes_sessions_and_pending_invitation(self):
        account, password, session = self.active_staff("Admission")
        self.mutate(account, "role", {"role": "Support"}, "PATCH")
        self.status(request("/auth/me", token=session["accessToken"]), 401)
        fresh = self.login(account["email"], password)
        self.status(request("/admin/work-queue", token=fresh["accessToken"]), 403)
        pending, token = self.invite()
        self.mutate(pending, "role", {"role": "Practitioner"}, "PATCH")
        self.status(request("/auth/staff/activate", "POST", {"token": token, "password": secrets.token_hex(24)}), 400)

    def test_05_concurrent_version_and_refresh_revocation(self):
        account, _, session = self.active_staff()
        path = "/admin/staff/" + account["id"] + "/role"
        body = {"role": "Practitioner", "expectedVersion": account["version"]}
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(lambda _: request(path, "PATCH", body, self.admin["accessToken"]), range(2)))
        self.assertEqual(sorted(result[0] for result in results), [200, 409])
        account.update(next(result[1]["data"] for result in results if result[0] == 200))
        # Race refresh with revocation on a fresh session. Both valid serializations must revoke old/rotated tokens.
        race_account, _, race = self.active_staff()
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            refresh = pool.submit(request, "/auth/refresh", "POST", {"refreshToken": race["refreshToken"]})
            revoke = pool.submit(request, "/admin/staff/" + race_account["id"] + "/revoke-sessions", "POST",
                                 {"expectedVersion": race_account["version"]}, self.admin["accessToken"])
            refreshed, revoked = refresh.result(), revoke.result()
        self.status(revoked, 200)
        self.assertTrue(refreshed[0] in (200, 401), "Refresh must serialize with revocation")
        self.status(request("/auth/me", token=race["accessToken"]), 401)
        if refreshed[0] == 200:
            rotated = refreshed[1]["data"]
            self.status(request("/auth/me", token=rotated["accessToken"]), 401)
            self.status(request("/auth/refresh", "POST", {"refreshToken": rotated["refreshToken"]}), 401)

    def test_06_admin_target_filters_and_audit_safety(self):
        me = self.status(request("/auth/me", token=self.admin["accessToken"]), 200)
        self.status(request("/admin/staff/" + me["userId"] + "/revoke-sessions", "POST",
            {"expectedVersion": me["accountVersion"]}, self.admin["accessToken"]), 403)
        self.status(request("/admin/staff/invitations", "POST",
            {"email": "synthetic-h02-admin@example.invalid", "role": "Administrator"}, self.admin["accessToken"]), 400)
        self.status(request("/admin/staff?limit=101", token=self.admin["accessToken"]), 400)
        default_page = self.status(request("/admin/staff", token=self.admin["accessToken"]), 200)
        self.assertLessEqual(len(default_page["items"]), 20)
        self.assertTrue("next" in default_page)
        filtered = self.status(request("/admin/staff?role=Admission&status=Active&limit=100",
                                       token=self.admin["accessToken"]), 200)
        self.assertTrue(bool(filtered["items"]), "Active admission fixture must appear in a filtered page")
        self.assertTrue(all(item["role"] == "Admission" and item["status"] == "Active"
                            for item in filtered["items"]))
        self.status(request("/admin/staff?role=Administrator", token=self.admin["accessToken"]), 400)
        self.status(request("/admin/staff?status=Unknown", token=self.admin["accessToken"]), 400)
        accounts = self.status(request("/admin/staff?limit=100", token=self.admin["accessToken"]), 200)
        self.assertTrue(all(item["role"] in ("Admission", "Practitioner", "Support") for item in accounts["items"]))
        page = self.status(request("/admin/staff/audit-events?limit=100", token=self.admin["accessToken"]), 200)
        self.assertTrue(all(str(event["action"]).startswith("staff.") for event in page["items"]))
        self.assertTrue(all(set(event) == {"sequence", "id", "actorId", "action", "resourceId", "occurredAt", "result", "traceId"}
                            for event in page["items"]), "Audit exposes only approved metadata")

    def test_07_patient_regression_and_server_rejection(self):
        email, password = "synthetic-h02-patient-" + uuid.uuid4().hex + "@example.invalid", secrets.token_hex(24)
        self.status(request("/auth/register", "POST", {"email": email, "password": password, "role": "Administrator"}), 202)
        self.status(request("/auth/verify", "POST", {"email": email, "token": self.mailbox(email, "verify")}), 200)
        patient = self.login(email, password)
        me = self.status(request("/auth/me", token=patient["accessToken"]), 200)
        self.assertEqual(me["role"], "Patient")
        self.status(request("/patients/me", token=patient["accessToken"]), 200)
        self.status(request("/appointments/me", token=patient["accessToken"]), 200)
        self.status(request("/admin/staff", token=patient["accessToken"]), 403)
        self.status(request("/admin/staff/" + me["userId"] + "/active", "PATCH",
                            {"active": False, "expectedVersion": me["accountVersion"]}, self.admin["accessToken"]), 404)

    def test_08_concurrent_activation_and_suspend_login_refresh(self):
        account, token = self.invite()
        password = secrets.token_hex(24)
        payload = {"token": token, "password": password}
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            results = list(pool.map(lambda _: request("/auth/staff/activate", "POST", payload), range(2)))
        self.assertEqual(sorted(result[0] for result in results), [200, 400])
        account.update({"version": account["version"] + 1, "status": "Active"})
        original = self.login(account["email"], password)
        barrier = threading.Barrier(3)

        def run(path, method, body, token=None):
            barrier.wait(timeout=10)
            return request(path, method, body, token)

        with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
            login = pool.submit(run, "/auth/login", "POST", {"email": account["email"], "password": password})
            refresh = pool.submit(run, "/auth/refresh", "POST", {"refreshToken": original["refreshToken"]})
            suspend = pool.submit(run, "/admin/staff/" + account["id"] + "/active", "PATCH",
                                  {"active": False, "expectedVersion": account["version"]}, self.admin["accessToken"])
            logged, refreshed, suspended = login.result(), refresh.result(), suspend.result()
        self.status(suspended, 200)
        for result in (logged, refreshed):
            self.assertTrue(result[0] in (200, 401), "Identity operations must serialize with suspension")
            if result[0] == 200:
                self.status(request("/auth/me", token=result[1]["data"]["accessToken"]), 401)
                self.status(request("/auth/refresh", "POST",
                                    {"refreshToken": result[1]["data"]["refreshToken"]}), 401)


if __name__ == "__main__":
    unittest.main()
