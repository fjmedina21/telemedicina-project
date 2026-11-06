using Telemedicina.Common.Models.ApiResponses;
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
[ProducesResponseType(typeof(ErrorApiResponse),400)]
[ProducesResponseType(typeof(ErrorApiResponse),401)]
[ProducesResponseType(typeof(ErrorApiResponse),403)]
[ProducesResponseType(typeof(ErrorApiResponse),409)]
public sealed class AdmissionController(Database database, NotificationService notifications) : ApiControllerBase(database)
{
    [Authorize(Roles = "Admission"), HttpGet("admin/work-queue")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentSnapshot>>),200)]
    public IActionResult Queue([FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? status = null, [FromQuery] string? coverageStatus = null)
    {
        var error=AppointmentFilters.Parse(from,to,status,coverageStatus,out var filters);
        if(error is not null) return Fail(400,error,"Revise los filtros de citas.");
        using var c = Database.Open();
        return Data(Rows(c, null, AppointmentQueries.Select + " WHERE true " + AppointmentFilters.Sql +
            " ORDER BY a.start_utc DESC,a.id LIMIT 200",filters.Parameters(DateTime.UtcNow)));
    }
    [Authorize(Roles = "Admission"), HttpPatch("admin/appointments/{id:guid}/validation")]
    [ProducesResponseType(typeof(ApiResponse<CoverageReviewed>),200)]
    [ProducesResponseType(404)]
    public IActionResult Review(Guid id, CoverageRequest request)
    {
        using var c = Database.Open(); using var t = c.BeginTransaction();
        // Appointment mutations lock the patient account before the appointment.
        // Discover its owner without a row lock, then acquire both accounts in
        // the shared deterministic order before taking the appointment lock.
        var owner = Scalar(c, t, "SELECT user_id FROM appointments WHERE id=@id", ("id", id.ToString())) as string;
        if (owner is null) return NotFound();
        Telemedicina.Api.Infrastructure.Authentication.AccountGuard.LockUsers(c, t, UserId(User), owner);
        Telemedicina.Api.Infrastructure.Authentication.AccountGuard.Require(c, t, User);
        Execute(c, t, "SELECT set_config('telemedicina.actor',@actor,true)", ("actor", UserId(User)));
        var a = Rows(c, t, "SELECT * FROM appointments WHERE id=@id FOR UPDATE", ("id", id.ToString())).FirstOrDefault();
        if (a is null || (string)a["user_id"]! != owner) return NotFound();
        var nowUtc=DateTime.UtcNow;
        if ((int)a["version"]! != request.Version || (string)a["status"]! is not ("PendingValidation" or "Confirmed")
            || (DateTime)a["start_utc"]! <= nowUtc)
            return Fail(409, "INVALID_TRANSITION", "La cita cambió o ya no permite revisión.");
        if (string.IsNullOrWhiteSpace(request.Evidence) || (request.Status == "Approved" &&
            (request.ValidUntil is null || request.ValidUntil.Value.UtcDateTime < (DateTime)a["end_utc"]! || request.ValidUntil.Value.UtcDateTime <= nowUtc)))
            return Fail(400, "EVIDENCE_REQUIRED", "Registre evidencia y una vigencia que cubra la cita.");
        var next = request.Status == "Approved" ? "Confirmed" : "PendingValidation";
        Execute(c, t, """
            UPDATE appointments SET coverage=@coverage,status=@status,evidence=@e,coverage_valid_until=@until,
            reviewed_by=@actor,reviewed_at=now(),version=version+1 WHERE id=@id
            """, ("coverage", request.Status), ("status", next), ("e", request.Evidence.Trim()),
            ("until", request.Status=="Approved" ? request.ValidUntil?.UtcDateTime : null), ("actor", UserId(User)), ("id", id.ToString()));
        notifications.AppointmentEvent(c, t, id.ToString(), "reviewed");
        Log(c, t, "coverage." + request.Status.ToLowerInvariant(), id.ToString()); t.Commit();
        return Data(new { status = next, coverageStatus = request.Status, version = request.Version + 1 });
    }

}
