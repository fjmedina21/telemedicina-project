using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Features.Catalog;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Common.Models.ApiResponses;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Features.Scheduling;

[ApiController, Authorize(Roles = "Admission"), Route("api/v1")]
[ProducesResponseType(typeof(ErrorApiResponse), 400)]
[ProducesResponseType(typeof(ErrorApiResponse), 401)]
[ProducesResponseType(typeof(ErrorApiResponse), 403)]
[ProducesResponseType(typeof(ErrorApiResponse), 409)]
public sealed class SchedulesController(Database database) : ApiControllerBase(database)
{
    [HttpGet("admin/slots")]
    [ProducesResponseType(typeof(ApiResponse<AdminSlotPage>), 200)]
    public IActionResult List([FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? serviceId = null, [FromQuery] string? practitionerId = null,
        [FromQuery] string? blocked = null, [FromQuery] string? reserved = null,
        [FromQuery] string offset = "0", [FromQuery] string limit = "100")
    {
        if (!ScheduleInputs.TryId(serviceId, false, out var service) ||
            !ScheduleInputs.TryId(practitionerId, false, out var practitioner) ||
            !ScheduleInputs.TryBoolean(blocked, out var isBlocked) || !ScheduleInputs.TryBoolean(reserved, out var isReserved) ||
            !int.TryParse(offset, NumberStyles.None, CultureInfo.InvariantCulture, out var skip) || skip < 0 ||
            !int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out var take) || take < 1 || take > 300)
            return Fail(400, "VALIDATION_FAILED", "Revise los filtros de agenda.");
        var error = ScheduleInputs.Range(from, to, DateTimeOffset.UtcNow, out var range);
        if (error is not null) return Fail(400, error, "Seleccione un rango válido de hasta 31 días.");
        using var c = Database.Open();
        var row = Rows(c, null, """
            WITH slots_with_state AS MATERIALIZED (
                SELECT s.id,s.service_id AS "serviceId",v.name AS "serviceName",
                    s.practitioner_id AS "practitionerId",p.name AS "practitionerName",
                    s.start_utc AS "startAt",s.end_utc AS "endAt",s.blocked,s.version,
                    EXISTS(SELECT 1 FROM appointments a WHERE a.slot_id=s.id AND a.status IN ('PendingValidation','Confirmed')) AS reserved,
                    v.active AS "serviceActive",p.active AS "practitionerActive",
                    EXISTS(SELECT 1 FROM practitioner_services ps WHERE ps.service_id=v.id AND ps.practitioner_id=p.id AND ps.active) AS "assignmentActive",
                    (v.active AND (v.is_synthetic OR length(btrim(coalesce(v.approval_reference,'')))>0)) AS "servicePublishable"
                FROM slots s JOIN services v ON v.id=s.service_id JOIN practitioners p ON p.id=s.practitioner_id
                WHERE s.start_utc>=@from AND s.start_utc<@to
                    AND (@service='' OR s.service_id=@service) AND (@person='' OR s.practitioner_id=@person)
            ), filtered AS MATERIALIZED (
                SELECT * FROM slots_with_state WHERE (@blocked::boolean IS NULL OR blocked=@blocked::boolean)
                    AND (@reserved::boolean IS NULL OR reserved=@reserved::boolean)
            )
            SELECT (SELECT count(*) FROM filtered) AS total_count,
                coalesce((SELECT jsonb_agg(to_jsonb(page) ORDER BY page."startAt",page.id)
                    FROM (SELECT * FROM filtered ORDER BY "startAt",id OFFSET @offset LIMIT @limit) page),'[]'::jsonb)::text AS items
            """, ("from", range.FromUtc), ("to", range.ToUtc), ("service", service), ("person", practitioner),
            ("blocked", isBlocked), ("reserved", isReserved), ("offset", skip), ("limit", take)).Single();
        var items = JsonSerializer.Deserialize<List<AdminSlotItem>>((string)row["items"]!, Json)!;
        var count = (long)row["total_count"]!;
        return Data(new AdminSlotPage(items, count, skip, take, ((long)skip + items.Count) < count));
    }

    [HttpPost("admin/slots")]
    [ProducesResponseType(typeof(ApiResponse<SlotCreated>), 201)]
    [ProducesResponseType(typeof(ErrorApiResponse), 404)]
    public IActionResult AddSlot(SlotRequest request)
    {
        if (request.ServiceId == Guid.Empty || request.PractitionerId == Guid.Empty)
            return Fail(400, "VALIDATION_FAILED", "Seleccione servicio y profesional válidos.");
        if (request.StartAt <= DateTimeOffset.UtcNow) return Fail(400, "INVALID_DATE", "Seleccione una fecha futura.");
        using var c = Database.Open(); using var t = Begin(c);
        var service = request.ServiceId.ToString(); var person = request.PractitionerId.ToString();
        OfferPolicy.LockServices(c, t, [service]);
        OfferPolicy.LockPractitioners(c, t, [person]);
        var offer = OfferPolicy.Read(c, t, service, person);
        if (offer is null || !offer.ServiceActive || !offer.PractitionerActive) return NotFound();
        if (!offer.Published) return Fail(409, "SLOT_UNAVAILABLE", "El servicio no está publicado.");
        if (!offer.Assigned) return Fail(400, "PRACTITIONER_SERVICE_REQUIRED", "El profesional no está asignado al servicio.");
        if (request.StartAt <= DateTimeOffset.UtcNow) return Fail(400, "INVALID_DATE", "Seleccione una fecha futura.");
        DateTime end;
        try { end = request.StartAt.UtcDateTime.AddMinutes(offer.Duration); }
        catch (ArgumentOutOfRangeException) { return Fail(400, "INVALID_DATE", "Seleccione una fecha válida."); }
        var id = Guid.NewGuid().ToString();
        try
        {
            Execute(c, t, "INSERT INTO slots(id,service_id,practitioner_id,start_utc,end_utc) VALUES(@id,@s,@p,@start,@end)",
                ("id", id), ("s", service), ("p", person), ("start", request.StartAt.UtcDateTime),
                ("end", end));
        }
        catch (Npgsql.PostgresException e) when (e.SqlState is "23P01" or "23505")
        {
            return Fail(409, "SCHEDULE_OVERLAP", "El profesional ya tiene un horario que solapa.");
        }
        Log(c, t, "schedule.slot_created", id); t.Commit(); return Data(new SlotCreated(id, 1), 201);
    }

    [HttpPatch("admin/slots/{id:guid}/block")]
    [ProducesResponseType(typeof(ApiResponse<SlotBlocked>), 200)]
    [ProducesResponseType(typeof(ErrorApiResponse), 404)]
    public IActionResult Block(Guid id, BlockRequest request)
    {
        if (request.Version is < 1) return Fail(400, "VALIDATION_FAILED", "Revise la versión del horario.");
        using var c = Database.Open(); using var t = Begin(c);
        var ids = OfferPolicy.Identities(c, t, [id.ToString()]);
        if (ids.Count == 0) return NotFound();
        OfferPolicy.LockOffers(c, t, ids);
        OfferPolicy.LockSlots(c, t, [id.ToString()]);
        var slot = Rows(c, t, "SELECT * FROM slots WHERE id=@id", ("id", id.ToString())).SingleOrDefault();
        if (slot is null) return NotFound();
        var version = (int)slot["version"]!;
        if (request.Version is not null && request.Version != version)
            return Fail(409, "VERSION_CONFLICT", "El horario cambió. Recargue antes de continuar.");
        var current = (bool)slot["blocked"]!;
        var occupied = (bool)Scalar(c, t, "SELECT EXISTS(SELECT 1 FROM appointments WHERE slot_id=@id AND status IN ('PendingValidation','Confirmed'))", ("id", id.ToString()))!;
        if (occupied && (request.Blocked || current != request.Blocked))
            return Fail(409, "SLOT_RESERVED", "Gestione primero las citas existentes.");
        if (current == request.Blocked) return Data(new SlotBlocked(current, version));
        if (!request.Blocked)
        {
            if ((DateTime)slot["start_utc"]! <= DateTime.UtcNow) return Fail(400, "INVALID_DATE", "El horario ya no es futuro.");
            var offer = OfferPolicy.Read(c, t, (string)slot["service_id"]!, (string)slot["practitioner_id"]!);
            if (offer is null || !offer.Published || !offer.PractitionerActive || !offer.Assigned)
                return Fail(409, "SLOT_UNAVAILABLE", "La oferta del horario ya no está disponible.");
        }
        Execute(c, t, "UPDATE slots SET blocked=@b,version=version+1 WHERE id=@id", ("b", request.Blocked), ("id", id.ToString()));
        Log(c, t, "schedule.block_changed", id.ToString()); t.Commit(); return Data(new SlotBlocked(request.Blocked, version + 1));
    }
}
