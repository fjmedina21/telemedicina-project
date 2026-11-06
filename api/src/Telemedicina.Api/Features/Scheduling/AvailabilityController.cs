using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Features.Catalog;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Common.Models.ApiResponses;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Features.Scheduling;

[ApiController, Authorize(Roles = "Patient,Admission"), Route("api/v1")]
public sealed class AvailabilityController(Database database) : ApiControllerBase(database)
{
    [HttpGet("availability")]
    [ProducesResponseType(typeof(ApiResponse<List<AvailabilityItem>>), 200)]
    [ProducesResponseType(typeof(ErrorApiResponse), 400)]
    [ProducesResponseType(typeof(ErrorApiResponse), 401)]
    [ProducesResponseType(typeof(ErrorApiResponse), 403)]
    public IActionResult Availability([FromQuery] string? serviceId = null, [FromQuery] string? from = null,
        [FromQuery] string? to = null, [FromQuery] string? practitionerId = null)
    {
        if (!ScheduleInputs.TryId(serviceId, true, out var service) || !ScheduleInputs.TryId(practitionerId, false, out var person))
            return Fail(400, "VALIDATION_FAILED", "Revise los filtros de disponibilidad.");
        var error = ScheduleInputs.Range(from, to, DateTimeOffset.UtcNow, out var range);
        if (error is not null) return Fail(400, error, "Seleccione un rango válido de hasta 31 días.");
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT s.id,s.service_id AS "serviceId",p.id AS "practitionerId",p.name AS "practitionerName",
                s.start_utc AS "startAt",s.end_utc AS "endAt",v.name AS "serviceName",v.specialty
            FROM slots s JOIN services v ON v.id=s.service_id JOIN practitioners p ON p.id=s.practitioner_id
            WHERE s.service_id=@sid AND
            """ + " " + OfferPolicy.PublicServiceSql + " " + """
             AND p.active AND NOT s.blocked
            AND EXISTS(SELECT 1 FROM practitioner_services ps WHERE ps.practitioner_id=p.id AND ps.service_id=v.id AND ps.active)
            AND s.start_utc>now() AND s.start_utc>=@from AND s.end_utc<=@to
            AND (@pid='' OR p.id=@pid)
            AND NOT EXISTS(SELECT 1 FROM appointments a WHERE a.slot_id=s.id AND a.status IN ('PendingValidation','Confirmed'))
            ORDER BY s.start_utc,p.name,s.id LIMIT 300
            """, ("sid", service), ("from", range.FromUtc), ("to", range.ToUtc), ("pid", person)));
    }
}
