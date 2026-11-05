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

[ApiController, Authorize, Route("api/v1")]
public sealed class SchedulesController(Database database) : ApiControllerBase(database)
{
    [Authorize(Roles = "Admission"), HttpPost("admin/slots")]
    public IActionResult AddSlot(SlotRequest request)
    {
        if (request.StartAt <= DateTimeOffset.UtcNow) return Fail(400, "INVALID_DATE", "Seleccione una fecha futura.");
        using var c = Database.Open(); using var t = Begin(c);
        var duration = Scalar(c, t, "SELECT duration FROM services WHERE id=@id AND active FOR SHARE", ("id", request.ServiceId.ToString()));
        if (duration is null || Scalar(c, t, "SELECT id FROM practitioners WHERE id=@id AND active FOR SHARE",
            ("id", request.PractitionerId.ToString())) is null) return NotFound();
        if (Scalar(c, t, "SELECT practitioner_id FROM practitioner_services WHERE practitioner_id=@p AND service_id=@s AND active FOR SHARE",
            ("p", request.PractitionerId.ToString()), ("s", request.ServiceId.ToString())) is null)
            return Fail(400, "PRACTITIONER_SERVICE_REQUIRED", "El profesional no está asignado al servicio.");
        var id = Guid.NewGuid().ToString();
        try
        {
            Execute(c, t, "INSERT INTO slots(id,service_id,practitioner_id,start_utc,end_utc) VALUES(@id,@s,@p,@start,@end)",
                ("id", id), ("s", request.ServiceId.ToString()), ("p", request.PractitionerId.ToString()),
                ("start", request.StartAt.UtcDateTime), ("end", request.StartAt.AddMinutes((int)duration).UtcDateTime));
        }
        catch (Npgsql.PostgresException e) when (e.SqlState == "23P01")
        {
            return Fail(409, "SCHEDULE_OVERLAP", "El profesional ya tiene un horario que solapa.");
        }
        Log(c, t, "schedule.slot_created", id); t.Commit(); return Data(new { id }, 201);
    }
    [Authorize(Roles = "Admission"), HttpPatch("admin/slots/{id:guid}/block")]
    public IActionResult Block(Guid id, BlockRequest request)
    {
        using var c = Database.Open(); using var t = Begin(c);
        if (Scalar(c, t, "SELECT id FROM slots WHERE id=@id FOR UPDATE", ("id", id.ToString())) is null) return NotFound();
        if (request.Blocked && Scalar(c, t, "SELECT id FROM appointments WHERE slot_id=@id AND status IN ('PendingValidation','Confirmed')",
            ("id", id.ToString())) is not null) return Fail(409, "SLOT_RESERVED", "Gestione primero las citas existentes.");
        Execute(c, t, "UPDATE slots SET blocked=@b WHERE id=@id", ("b", request.Blocked), ("id", id.ToString()));
        Log(c, t, "schedule.block_changed", id.ToString()); t.Commit(); return Data(new { blocked = request.Blocked });
    }

}
