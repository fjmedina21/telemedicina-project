using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Api.Shared.Contracts;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;
using Telemedicina.Api.Features.Appointments;
using Telemedicina.Api.Features.Notifications;

namespace Telemedicina.Api.Features.Admission;

[ApiController, Authorize, Route("api/v1")]
public sealed class AdmissionController(Database database, NotificationService notifications) : ApiControllerBase(database)
{
    [Authorize(Roles = "Admission"), HttpGet("admin/work-queue")]
    public IActionResult Queue()
    {
        using var c = Database.Open();
        return Data(Rows(c, null, AppointmentQueries.Select + " ORDER BY a.start_utc DESC LIMIT 200"));
    }
    [Authorize(Roles = "Admission"), HttpPatch("admin/appointments/{id:guid}/validation")]
    public IActionResult Review(Guid id, CoverageRequest request)
    {
        using var c = Database.Open(); using var t = Begin(c);
        var a = Rows(c, t, "SELECT * FROM appointments WHERE id=@id FOR UPDATE", ("id", id.ToString())).FirstOrDefault();
        if (a is null) return NotFound();
        if ((int)a["version"]! != request.Version || (string)a["status"]! is not ("PendingValidation" or "Confirmed")
            || (DateTime)a["start_utc"]! <= DateTime.UtcNow)
            return Fail(409, "INVALID_TRANSITION", "La cita cambió o ya no permite revisión.");
        if (string.IsNullOrWhiteSpace(request.Evidence) || (request.Status == "Approved" &&
            (request.ValidUntil is null || request.ValidUntil.Value.UtcDateTime < (DateTime)a["end_utc"]!)))
            return Fail(400, "EVIDENCE_REQUIRED", "Registre evidencia y una vigencia que cubra la cita.");
        var next = request.Status == "Approved" ? "Confirmed" : "PendingValidation";
        Execute(c, t, """
            UPDATE appointments SET coverage=@coverage,status=@status,evidence=@e,coverage_valid_until=@until,
            reviewed_by=@actor,reviewed_at=now(),version=version+1 WHERE id=@id
            """, ("coverage", request.Status), ("status", next), ("e", request.Evidence.Trim()),
            ("until", request.ValidUntil?.UtcDateTime), ("actor", UserId(User)), ("id", id.ToString()));
        notifications.AppointmentEvent(c, t, id.ToString(), "reviewed");
        Log(c, t, "coverage." + request.Status.ToLowerInvariant(), id.ToString()); t.Commit();
        return Data(new { status = next, coverageStatus = request.Status, version = request.Version + 1 });
    }

}
