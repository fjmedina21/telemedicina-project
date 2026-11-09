using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Features.Patients;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Common.Models.ApiResponses;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Features.Appointments;

[ApiController, Authorize, Route("api/v1")]
[ProducesResponseType(typeof(ErrorApiResponse), 400)]
[ProducesResponseType(typeof(ErrorApiResponse), 401)]
[ProducesResponseType(typeof(ErrorApiResponse), 403)]
[ProducesResponseType(typeof(ErrorApiResponse), 409)]
public sealed class CheckInController(Database database) : ApiControllerBase(database)
{
    [Authorize(Roles = "Patient")]
    [HttpGet("appointments/{id:guid}/check-in-requirements")]
    [ProducesResponseType(typeof(ApiResponse<PatientCheckInSnapshot>), 200)]
    [ProducesResponseType(404)]
    public IActionResult PatientRequirements(Guid id)
    {
        using var c = Database.Open(); using var t = Begin(c);
        // Query endpoints are pure reads. Share locks provide one scheduling
        // snapshot; expiration/invalidation are projected, never persisted here.
        var appointment = LockAppointment(c, t, id, false, false);
        if (appointment is null) return NotFound();
        var attempt = LockAttempt(c, t, id, false);
        var result = PatientSnapshot(c, t, appointment, attempt, DateTime.UtcNow);
        Log(c, t, "appointment.check_in_requirements.read", id.ToString());
        t.Commit(); return Data(result);
    }

    [Authorize(Roles = "Admission")]
    [HttpGet("admission/appointments/{id:guid}/check-in-status")]
    [ProducesResponseType(typeof(ApiResponse<AdmissionCheckInStatus>), 200)]
    [ProducesResponseType(404)]
    public IActionResult AdmissionStatus(Guid id)
    {
        using var c = Database.Open(); using var t = Begin(c);
        // Redacted Admission query follows the same read-only snapshot rule.
        var appointment = LockAppointment(c, t, id, true, false);
        if (appointment is null) return NotFound();
        var attempt = LockAttempt(c, t, id, false);
        var now = DateTime.UtcNow;
        var eligibility = Eligibility(c, t, (string)appointment["user_id"]!);
        var status = EffectiveState(appointment, attempt, eligibility, now);
        var result = new AdmissionCheckInStatus(id.ToString(), status,
            CheckInPolicy.ConfigurationVersion, attempt?["version"] as int? ?? 0,
            CheckInPolicy.OpensAt((DateTime)appointment["start_utc"]!),
            CheckInPolicy.ClosesAt((DateTime)appointment["start_utc"]!),
            attempt?["updated_at"] as DateTime?, attempt?["ready_at"] as DateTime?);
        Log(c, t, "appointment.check_in_status.read", id.ToString());
        t.Commit(); return Data(result);
    }

    [Authorize(Roles = "Patient")]
    [HttpPost("appointments/{id:guid}/check-in")]
    [ProducesResponseType(typeof(ApiResponse<PatientCheckInSnapshot>), 200)]
    [ProducesResponseType(404)]
    public IActionResult Submit(Guid id, CheckInRequest request)
    {
        var key = Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParse(key, out _))
            return Fail(400, "IDEMPOTENCY_REQUIRED", "Envíe Idempotency-Key como UUID.");
        if (request.ConsentVersion != CheckInPolicy.ConsentVersion)
            return Fail(409, "CONSENT_VERSION_CHANGED", "Recargue el consentimiento académico vigente.");
        var payload = Hash("check-in:" + id + ":" + JsonSerializer.Serialize(request, Json));
        using var c = Database.Open(); using var t = Begin(c);
        Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",
            ("key", UserId(User) + ":" + key));
        var old = Rows(c, t, "SELECT request_hash FROM idempotency WHERE user_id=@u AND key=@key",
            ("u", UserId(User)), ("key", key));
        if (old.Count > 0 && (string)old[0]["request_hash"]! != payload)
            return Fail(409, "IDEMPOTENCY_MISMATCH", "La clave ya se usó con otra solicitud.");

        var appointment = LockAppointment(c, t, id, false, true);
        if (appointment is null) return NotFound();
        var attempt = CloseStaleAttempt(c, t, appointment, LockAttempt(c, t, id, true));
        var now = DateTime.UtcNow;
        var start = (DateTime)appointment["start_utc"]!;
        if (old.Count > 0)
        {
            // Never replay the serialized historical snapshot. Recompose under
            // the appointment/attempt locks and reject a cancelled, rescheduled
            // or otherwise invalidated command target.
            if ((string)appointment["status"]! != "Confirmed" || attempt is null ||
                attempt["active"] is false)
                return Fail(409, "CHECK_IN_INVALIDATED",
                    "El check-in asociado a esta operación ya no es vigente.");
            RevalidateProfile(c, t, attempt,
                Eligibility(c, t, (string)appointment["user_id"]!), now);
            var replay = PatientSnapshot(c, t, appointment, attempt, now);
            Log(c, t, "appointment.check_in.replayed", id.ToString());
            t.Commit(); return Data(replay);
        }
        if ((string)appointment["status"]! != "Confirmed")
            return Fail(409, "APPOINTMENT_NOT_CONFIRMED", "La cita debe estar confirmada.");
        if (!CheckInPolicy.InWindow(start, now))
            return Fail(409, now < CheckInPolicy.OpensAt(start) ? "CHECK_IN_NOT_OPEN" : "CHECK_IN_EXPIRED",
                now < CheckInPolicy.OpensAt(start) ? "El check-in abre treinta minutos antes." : "La ventana de check-in finalizó.");
        var eligibility = Eligibility(c, t, UserId(User));
        if (!eligibility.Complete)
            return Fail(409, "PROFILE_INCOMPLETE", "Complete el perfil antes del check-in.");
        if (!eligibility.Adult)
            return Fail(409, "ADULT_SELF_REQUIRED", "Esta demostración solo admite adultos actuando por cuenta propia.");

        if (attempt is not null && attempt["active"] is false) attempt = null;
        if (attempt is null && request.Version != 0 ||
            attempt is not null && (int)attempt["version"]! != request.Version)
            return Fail(409, "CHECK_IN_VERSION_CONFLICT", "El check-in cambió. Recargue su estado.");
        var accepted = attempt?["consent_version"] is not null || request.ConsentAccepted;
        var state = CheckInPolicy.State("Confirmed", start, now, true,
            request.Prepared, request.PrivacyConfirmed, request.ConnectivityConfirmed,
            accepted, request.TechnicalPreflightCompleted);
        var attemptId = attempt?["id"] as string ?? Guid.NewGuid().ToString();
        if (attempt is null)
            Execute(c, t, """
                INSERT INTO check_in_attempts(id,appointment_id,user_id,appointment_version,state,
                    profile_version,prepared,privacy_confirmed,connectivity_confirmed,technical_preflight_completed,
                    consent_version,consent_hash,ready_at)
                VALUES(@id,@appointment,@user,@appointmentVersion,@state,@profileVersion,@prepared,@privacy,
                    @connectivity,@preflight,@consentVersion,@consentHash,@readyAt)
                """, ("id", attemptId), ("appointment", id.ToString()), ("user", UserId(User)),
                ("appointmentVersion", appointment["version"]), ("state", state),
                ("profileVersion", eligibility.Version),
                ("prepared", request.Prepared), ("privacy", request.PrivacyConfirmed),
                ("connectivity", request.ConnectivityConfirmed), ("preflight", request.TechnicalPreflightCompleted),
                ("consentVersion", accepted ? CheckInPolicy.ConsentVersion : null),
                ("consentHash", accepted ? CheckInPolicy.ConsentHash : null),
                ("readyAt", state == "Ready" ? now : null));
        else
            Execute(c, t, """
                UPDATE check_in_attempts SET state=@state,prepared=@prepared,
                    privacy_confirmed=@privacy,connectivity_confirmed=@connectivity,
                    technical_preflight_completed=@preflight,consent_version=@consentVersion,
                    consent_hash=@consentHash,profile_version=@profileVersion,
                    version=version+1,updated_at=@now,
                    ready_at=CASE WHEN @state='Ready' THEN coalesce(ready_at,@now) ELSE NULL END
                WHERE id=@id
                """, ("state", state), ("prepared", request.Prepared), ("privacy", request.PrivacyConfirmed),
                ("connectivity", request.ConnectivityConfirmed), ("preflight", request.TechnicalPreflightCompleted),
                ("consentVersion", accepted ? CheckInPolicy.ConsentVersion : null),
                ("consentHash", accepted ? CheckInPolicy.ConsentHash : null),
                ("profileVersion", eligibility.Version), ("now", now), ("id", attemptId));
        attempt = LockAttempt(c, t, id, true);
        var result = PatientSnapshot(c, t, appointment, attempt, now);
        Execute(c, t, "INSERT INTO idempotency(user_id,key,request_hash,response) VALUES(@u,@key,@hash,@response)",
            ("u", UserId(User)), ("key", key), ("hash", payload),
            ("response", JsonSerializer.Serialize(result, Json)));
        Log(c, t, state == "Ready" ? "appointment.check_in.ready" : "appointment.check_in.updated", id.ToString());
        t.Commit(); return Data(result);
    }

    private Dictionary<string, object?>? LockAppointment(Npgsql.NpgsqlConnection c,
        Npgsql.NpgsqlTransaction t, Guid id, bool staff, bool write) => Rows(c, t, $"""
        SELECT id,user_id,start_utc,status,version FROM appointments
        WHERE id=@id AND (@staff OR user_id=@user) {(write ? "FOR UPDATE" : "FOR SHARE")}
        """, ("id", id.ToString()), ("staff", staff), ("user", UserId(User))).FirstOrDefault();

    private static Dictionary<string, object?>? LockAttempt(Npgsql.NpgsqlConnection c,
        Npgsql.NpgsqlTransaction t, Guid id, bool write) => Rows(c, t, $"""
        SELECT * FROM check_in_attempts WHERE appointment_id=@id AND active
        {(write ? "FOR UPDATE" : "FOR SHARE")}
        """,
        ("id", id.ToString())).FirstOrDefault();

    private static Dictionary<string, object?>? CloseStaleAttempt(Npgsql.NpgsqlConnection c,
        Npgsql.NpgsqlTransaction t, Dictionary<string, object?> appointment,
        Dictionary<string, object?>? attempt)
    {
        if (attempt is null || (int)attempt["appointment_version"]! == (int)appointment["version"]!) return attempt;
        Execute(c, t, """
            UPDATE check_in_attempts SET active=false,state='Rejected',version=version+1,
                ready_at=NULL,invalidated_at=now(),updated_at=now() WHERE id=@id
            """, ("id", attempt["id"]));
        attempt["active"] = false; attempt["state"] = "Rejected";
        attempt["ready_at"] = null;
        attempt["version"] = (int)attempt["version"]! + 1;
        return attempt;
    }

    private static (bool Complete, bool Adult, int Version) Eligibility(Npgsql.NpgsqlConnection c,
        Npgsql.NpgsqlTransaction t, string user)
    {
        var row = Rows(c, t, "SELECT body,version FROM profiles WHERE user_id=@u", ("u", user)).FirstOrDefault();
        if (row is null) return (false, false, 0);
        try
        {
            var body = JsonSerializer.Deserialize<JsonElement>((string)row["body"]!);
            return (ProfileRules.Completeness(body).Status == "Complete",
                CheckInPolicy.IsAdultSelf(body, DateOnly.FromDateTime(DateTime.UtcNow)), (int)row["version"]!);
        }
        catch (JsonException) { return (false, false, 0); }
    }

    private static void RevalidateProfile(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t,
        Dictionary<string, object?>? attempt, (bool Complete, bool Adult, int Version) eligibility, DateTime now)
    {
        if (attempt is null || attempt["active"] is false || attempt["state"] as string != "Ready" ||
            eligibility.Complete && eligibility.Adult && (int)attempt["profile_version"]! == eligibility.Version) return;
        Execute(c, t, "UPDATE check_in_attempts SET state='InProgress',ready_at=NULL,version=version+1,updated_at=@now WHERE id=@id",
            ("now", now), ("id", attempt["id"]));
        attempt["state"] = "InProgress"; attempt["ready_at"] = null;
        attempt["version"] = (int)attempt["version"]! + 1; attempt["updated_at"] = now;
    }

    private static string EffectiveState(Dictionary<string, object?> appointment,
        Dictionary<string, object?>? attempt, (bool Complete, bool Adult, int Version) eligibility,
        DateTime now)
    {
        if (attempt is not null && attempt["active"] is false) return "Rejected";
        if (attempt is not null && (int)attempt["appointment_version"]! != (int)appointment["version"]!) return "Rejected";
        if ((string)appointment["status"]! != "Confirmed") return "Rejected";
        if (now > CheckInPolicy.ClosesAt((DateTime)appointment["start_utc"]!)) return "Expired";
        if (attempt?["state"] as string == "Ready" && (!eligibility.Complete || !eligibility.Adult ||
            (int)attempt["profile_version"]! != eligibility.Version)) return "InProgress";
        return attempt?["state"] as string ?? "NotStarted";
    }

    private static PatientCheckInSnapshot PatientSnapshot(Npgsql.NpgsqlConnection c,
        Npgsql.NpgsqlTransaction t, Dictionary<string, object?> appointment,
        Dictionary<string, object?>? attempt, DateTime now)
    {
        var eligibility = Eligibility(c, t, (string)appointment["user_id"]!);
        var status = EffectiveState(appointment, attempt, eligibility, now);
        var start = (DateTime)appointment["start_utc"]!;
        var prepared = attempt?["prepared"] is true;
        var privacy = attempt?["privacy_confirmed"] is true;
        var connectivity = attempt?["connectivity_confirmed"] is true;
        var preflight = attempt?["technical_preflight_completed"] is true;
        var consent = attempt?["consent_version"] is not null;
        var blockers = new List<string>();
        if ((string)appointment["status"]! != "Confirmed") blockers.Add("APPOINTMENT_NOT_CONFIRMED");
        if (!CheckInPolicy.InWindow(start, now)) blockers.Add("OUTSIDE_CHECK_IN_WINDOW");
        if (!eligibility.Complete) blockers.Add("PROFILE_INCOMPLETE");
        if (eligibility.Complete && !eligibility.Adult) blockers.Add("ADULT_SELF_REQUIRED");
        if (!(prepared && privacy && connectivity)) blockers.Add("ADMIN_CHECKLIST_INCOMPLETE");
        if (!consent) blockers.Add("DEMO_CONSENT_REQUIRED");
        if (!preflight) blockers.Add("DECLARATIVE_PREFLIGHT_REQUIRED");
        if (attempt?["active"] is false) blockers.Add("CHECK_IN_INVALIDATED");
        return new(status, (string)appointment["status"]! == "Confirmed" && eligibility.Complete &&
            eligibility.Adult && CheckInPolicy.InWindow(start, now) && attempt?["active"] is not false,
            CheckInPolicy.ConfigurationVersion, attempt?["version"] as int? ?? 0,
            CheckInPolicy.OpensAt(start), CheckInPolicy.ClosesAt(start), prepared, privacy, connectivity,
            preflight, new(CheckInPolicy.ConsentVersion, CheckInPolicy.ConsentHash,
                CheckInPolicy.ConsentText, consent), blockers.ToArray());
    }
}
