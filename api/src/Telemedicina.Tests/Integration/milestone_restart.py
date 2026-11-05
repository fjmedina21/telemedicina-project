"""Persistence smoke test with two actual API process lifetimes; synthetic development only."""
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import time
import urllib.error
import urllib.parse
import urllib.request

URL = "http://127.0.0.1:5092/api/v1"

def call(path, method="GET", body=None, token=None, headers=None):
    h = {"Content-Type": "application/json", "X-Telemedicina-Client": "milestone"}
    h.update(headers or {})
    if token:
        h["Authorization"] = "Bearer " + token
    request = urllib.request.Request(URL + path,
        data=None if body is None else json.dumps(body).encode(), headers=h, method=method)
    with urllib.request.urlopen(request, timeout=20) as response:
        return response.status, json.load(response).get("data")

def check_restart():
    assert os.environ.get("MILESTONE_TEST_SYNTHETIC") == "1", "Synthetic test opt-in required"
    try:
        connection = socket.create_connection(("127.0.0.1", 5092), timeout=0.5)
    except OSError:
        pass
    else:
        connection.close()
        raise RuntimeError("Port 5092 is in use; refusing to stop an unrelated process.")
    root = Path(__file__).resolve().parents[4]
    project = root / "api/src/Telemedicina.Api/Telemedicina.Api.csproj"
    server = None

    def start():
        process = subprocess.Popen(["dotnet", "run", "--no-build", "--no-launch-profile",
            "--configuration",os.environ.get("MILESTONE_CONFIGURATION","Debug"),
            "--project", str(project), "--", "--urls", "http://127.0.0.1:5092"],
            cwd=root, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            for _ in range(60):
                if process.poll() is not None:
                    raise RuntimeError("Persistence test API exited before readiness.")
                try:
                    if call("/health")[0] == 200:
                        return process
                except (OSError, urllib.error.URLError):
                    time.sleep(0.25)
            raise RuntimeError("Persistence test API readiness timed out.")
        except BaseException:
            process.terminate(); process.wait(timeout=10)
            raise

    def stop(process):
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill(); process.wait(timeout=10)

    try:
        server = start()
        email = "synthetic-restart-" + secrets.token_hex(8) + "@example.invalid"
        password = secrets.token_urlsafe(24)
        assert call("/auth/register", "POST", {"email": email, "password": password})[0] == 202
        _, challenge = call("/auth/dev-mailbox?" + urllib.parse.urlencode({"email": email, "purpose": "verify"}),
            headers={"X-Dev-Mailbox-Key": os.environ["Milestone__MailboxKey"]})
        call("/auth/verify", "POST", {"email": email, "token": challenge["token"]})
        _, session = call("/auth/login", "POST", {"email": email, "password": password})
        token = session["accessToken"]
        profile = {"firstName": "Paciente", "lastName": "Reinicio sintético", "birthDate": "1990-01-01",
            "phone": "8095550101", "address": "Dirección de prueba", "emergencyName": "Contacto sintético",
            "emergencyPhone": "8095550102", "allergies": [], "medications": [], "conditions": [], "version": 0}
        call("/patients/me", "PATCH", profile, token)
        _, services = call("/services", token=token)
        _, slots = call("/availability?serviceId=" + services[0]["id"], token=token)
        assert slots, "Seed future slots before running this test."
        payload = {"slotId": slots[0]["id"], "insurer": "Aseguradora sintética", "memberLast4": "0000"}
        key = {"Idempotency-Key": "f0" + secrets.token_hex(3) + "-1234-4321-8123-" + secrets.token_hex(6)}
        _, appointment = call("/appointments", "POST", payload, token, key)
        stop(server); server = None
        server = start()
        _, detail = call("/appointments/" + appointment["id"], token=token)
        assert detail["status"] == "PendingValidation"
        assert detail["coverageStatus"] == "ManualReview"
        status, repeated = call("/appointments", "POST", payload, token, key)
        assert status == 200 and repeated["id"] == appointment["id"]
        _, profile_after = call("/patients/me", token=token)
        assert profile_after["profile"]["lastName"] == profile["lastName"]
        call("/appointments/" + appointment["id"] + "/cancel", "POST", {"version": 1}, token)
        call("/auth/logout", "POST", {}, token)
    finally:
        if server is not None:
            stop(server)

if __name__ == "__main__":
    check_restart()
    print("Restart persistence: PASS")
