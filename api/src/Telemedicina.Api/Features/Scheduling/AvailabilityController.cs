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

namespace Telemedicina.Api.Features.Scheduling;

[ApiController, Authorize(Roles = "Patient,Admission"), Route("api/v1")]
public sealed class AvailabilityController(Database database) : ApiControllerBase(database)
{
    [HttpGet("availability")]
    public IActionResult Availability([FromQuery] Guid serviceId, [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null, [FromQuery] string? practitionerId = null)
    {
        var start = (from ?? DateTimeOffset.UtcNow).UtcDateTime;
        var end = (to ?? DateTimeOffset.UtcNow.AddDays(14)).UtcDateTime;
        if (end <= start || end - start > TimeSpan.FromDays(31)) return Fail(400, "INVALID_RANGE", "Rango máximo: 31 días.");
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT s.id,s.service_id AS "serviceId",p.id AS "practitionerId",p.name AS "practitionerName",
                s.start_utc AS "startAt",s.end_utc AS "endAt",v.name AS "serviceName",v.specialty
            FROM slots s JOIN services v ON v.id=s.service_id JOIN practitioners p ON p.id=s.practitioner_id
            WHERE s.service_id=@sid AND v.active AND p.active AND NOT s.blocked
            AND EXISTS(SELECT 1 FROM practitioner_services ps WHERE ps.practitioner_id=p.id AND ps.service_id=v.id AND ps.active)
            AND s.start_utc>now() AND s.start_utc>=@from AND s.end_utc<=@to
            AND (@pid='' OR p.id=@pid)
            AND NOT EXISTS(SELECT 1 FROM appointments a WHERE a.slot_id=s.id AND a.status IN ('PendingValidation','Confirmed'))
            ORDER BY s.start_utc,p.name LIMIT 300
            """, ("sid", serviceId.ToString()), ("from", start), ("to", end), ("pid", practitionerId ?? "")));
    }

}
