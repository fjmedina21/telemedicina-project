using Telemedicina.Api.Features.Patients;
using Telemedicina.Common.Models.ApiResponses;
using Telemedicina.Api.Features.Catalog;
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

namespace Telemedicina.Api.Features.Appointments;

[ApiController, Authorize(Roles = "Patient,Admission"), Route("api/v1")]
[ProducesResponseType(typeof(ErrorApiResponse),400)]
[ProducesResponseType(typeof(ErrorApiResponse),401)]
[ProducesResponseType(typeof(ErrorApiResponse),403)]
[ProducesResponseType(typeof(ErrorApiResponse),409)]
public sealed class AppointmentsController(Database database, NotificationService notifications) : ApiControllerBase(database)
{
    [Authorize(Roles = "Patient"), HttpGet("appointments/me")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentSnapshot>>),200)]
    public IActionResult Mine([FromQuery] string? from = null, [FromQuery] string? to = null,
        [FromQuery] string? status = null, [FromQuery] string? coverageStatus = null)
    {
        var error=AppointmentFilters.Parse(from,to,status,coverageStatus,out var filters);
        if(error is not null) return Fail(400,error,"Revise los filtros de citas.");
        using var c = Database.Open();
        return Data(Rows(c, null, AppointmentQueries.Select + " WHERE a.user_id=@u " + AppointmentFilters.Sql +
            " ORDER BY a.start_utc DESC,a.id LIMIT 100", filters.Parameters(DateTime.UtcNow).Append(("u",(object?)UserId(User))).ToArray()));
    }
    [HttpGet("appointments/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentSnapshot>),200)]
    [ProducesResponseType(404)]
    public IActionResult Detail(Guid id)
    {
        using var c = Database.Open(); using var t = Begin(c);
        var rows = Rows(c, t, AppointmentQueries.Select + " WHERE a.id=@id AND (@staff OR a.user_id=@u)",
            ("id", id.ToString()), ("staff", User.IsInRole("Admission")), ("u", UserId(User)), ("nowUtc", DateTime.UtcNow));
        if (rows.Count == 0) return NotFound();
        Log(c, t, "appointment.read", id.ToString()); t.Commit(); return Data(rows[0]);
    }

    [Authorize(Roles = "Patient"), HttpPost("appointments")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentBooked>),200)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentBooked>),201)]
    public IActionResult Book(BookRequest request)
    {
        if (request.SlotId == Guid.Empty) return Fail(400,"VALIDATION_FAILED","Seleccione un horario válido.");
        var key = Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParse(key, out _)) return Fail(400, "IDEMPOTENCY_REQUIRED", "Envíe Idempotency-Key como UUID.");
        if (string.IsNullOrWhiteSpace(request.Insurer)) return Fail(400, "INSURER_REQUIRED", "Indique aseguradora para revisión manual.");
        var payload = Hash(JsonSerializer.Serialize(request, Json));
        using var c = Database.Open(); using var t = Begin(c);
        // Serialize retries for this user/key before checking the stored result.
        Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", UserId(User) + ":" + key));
        var old = Rows(c, t, "SELECT request_hash,response FROM idempotency WHERE user_id=@u AND key=@k",
            ("u", UserId(User)), ("k", key));
        if (old.Count > 0) return (string)old[0]["request_hash"]! == payload
            ? Data(JsonSerializer.Deserialize<JsonElement>((string)old[0]["response"]!))
            : Fail(409, "IDEMPOTENCY_MISMATCH", "La clave ya se usó con otra solicitud.");
        var storedProfile=Scalar(c,t,"SELECT body FROM profiles WHERE user_id=@u",("u",UserId(User))) as string;
        if(storedProfile is null) return Fail(400,"PROFILE_REQUIRED","Complete el perfil antes de solicitar.");
        JsonElement profile;
        try { profile=JsonSerializer.Deserialize<JsonElement>(storedProfile); }
        catch(JsonException) { profile=default; }
        if(ProfileRules.Completeness(profile).Status!="Complete")
            return StatusCode(400,new ErrorApiResponse(400,"Complete el perfil antes de solicitar.",Request.Path.Value??"/",
                ProfileRules.ValidateBody(profile)) { Code="PROFILE_INCOMPLETE",TraceId=HttpContext.TraceIdentifier });
        var slots = AvailableSlots(c, t, request.SlotId.ToString());
        if (slots.Count == 0) return Fail(409, "SLOT_UNAVAILABLE", "Horario no disponible.");
        var slot = slots[0]; var id = Guid.NewGuid().ToString();
        try
        {
            Execute(c, t, """
                INSERT INTO appointments(id,user_id,slot_id,practitioner_id,start_utc,end_utc,status,coverage,insurer,member_last4)
                VALUES(@id,@u,@s,@p,@start,@end,'PendingValidation','ManualReview',@insurer,@member)
                """, ("id", id), ("u", UserId(User)), ("s", request.SlotId.ToString()), ("p", slot["practitioner_id"]),
                ("start", slot["start_utc"]), ("end", slot["end_utc"]), ("insurer", request.Insurer.Trim()), ("member", request.MemberLast4));
        }
        catch (Npgsql.PostgresException e) when (e.SqlState is "23505" or "23P01")
        {
            return Fail(409, "SLOT_UNAVAILABLE", "Otro paciente solicitó ese horario. Seleccione otro.");
        }
        var result = new { id, status = "PendingValidation", coverageStatus = "ManualReview", version = 1 };
        Execute(c, t, "INSERT INTO idempotency(user_id,key,request_hash,response) VALUES(@u,@k,@h,@r)",
            ("u", UserId(User)), ("k", key), ("h", payload), ("r", JsonSerializer.Serialize(result, Json)));
        notifications.AppointmentEvent(c, t, id, "requested");
        Log(c, t, "appointment.requested", id); t.Commit(); return Data(result, 201);
    }
    [HttpGet("appointments/{id:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentHistoryItem>>),200)]
    [ProducesResponseType(404)]
    public IActionResult History(Guid id)
    {
        using var c = Database.Open(); using var t = Begin(c);
        if (Scalar(c, t, "SELECT id FROM appointments WHERE id=@id AND (@staff OR user_id=@u)",
            ("id", id.ToString()), ("staff", User.IsInRole("Admission")), ("u", UserId(User))) is null) return NotFound();
        var rows = Rows(c, t, """
            SELECT sequence,old_slot_id AS "oldSlotId",new_slot_id AS "newSlotId",old_status AS "oldStatus",
            new_status AS "newStatus",old_coverage AS "oldCoverage",new_coverage AS "newCoverage",
            version,actor_id AS "actorId",occurred_at AS "occurredAt"
            FROM appointment_history WHERE appointment_id=@id ORDER BY sequence
            """, ("id", id.ToString()));
        Log(c, t, "appointment.history_read", id.ToString()); t.Commit(); return Data(rows);
    }
    [Authorize(Roles = "Patient"), HttpPost("appointments/{id:guid}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentCancelled>),200)]
    [ProducesResponseType(404)]
    public IActionResult Cancel(Guid id, VersionRequest request)
    {
        using var c = Database.Open(); using var t = Begin(c);
        var row = Rows(c, t, "SELECT * FROM appointments WHERE id=@id AND user_id=@u FOR UPDATE",
            ("id", id.ToString()), ("u", UserId(User))).FirstOrDefault();
        if (row is null) return NotFound();
        if ((int)row["version"]! != request.Version || (string)row["status"]! is not ("PendingValidation" or "Confirmed")
            || (DateTime)row["start_utc"]! <= DateTime.UtcNow)
            return Fail(409, "INVALID_TRANSITION", "La cita cambió o ya no permite cancelación.");
        Execute(c, t, """
            UPDATE check_in_attempts SET active=false,state='Rejected',version=version+1,
                ready_at=NULL,invalidated_at=now(),updated_at=now()
            WHERE appointment_id=@id AND active
            """, ("id", id.ToString()));
        Execute(c, t, "UPDATE appointments SET status='Cancelled',version=version+1 WHERE id=@id", ("id", id.ToString()));
        notifications.AppointmentEvent(c, t, id.ToString(), "cancelled");
        Log(c, t, "appointment.cancelled", id.ToString()); t.Commit(); return Data(new { cancelled = true });
    }
    [Authorize(Roles = "Patient"), HttpPost("appointments/{id:guid}/reschedule")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentRescheduled>),200)]
    [ProducesResponseType(404)]
    public IActionResult Reschedule(Guid id, RescheduleRequest request)
    {
        if(request.SlotId==Guid.Empty) return Fail(400,"VALIDATION_FAILED","Seleccione un horario válido.");
        using var c = Database.Open(); using var t = Begin(c);
        var a = Rows(c, t, "SELECT * FROM appointments WHERE id=@id AND user_id=@u FOR UPDATE",
            ("id", id.ToString()), ("u", UserId(User))).FirstOrDefault();
        if (a is null) return NotFound();
        if ((int)a["version"]! != request.Version || (string)a["status"]! is not ("PendingValidation" or "Confirmed") ||
            (DateTime)a["start_utc"]! <= DateTime.UtcNow)
            return Fail(409, "INVALID_TRANSITION", "La cita cambió o ya no permite reprogramación.");
        var originalSlot=(string)a["slot_id"]!;
        if(request.SlotId.ToString()==originalSlot) return Fail(409,"SAME_SLOT","Seleccione otro horario.");
        var identities=OfferPolicy.Identities(c,t,[originalSlot,request.SlotId.ToString()]);
        var original=identities.Single(row=>row.Id==originalSlot);
        var target=identities.SingleOrDefault(row=>row.Id==request.SlotId.ToString());
        if(target is null) return Fail(409,"SLOT_UNAVAILABLE","Horario no disponible.");
        if(target.ServiceId!=original.ServiceId) return Fail(409,"SERVICE_CHANGE_NOT_ALLOWED","La reprogramación debe conservar el servicio.");
        var s = AvailableSlots(c, t, request.SlotId.ToString(), originalSlot, id.ToString()).FirstOrDefault();
        if (s is null) return Fail(409, "SLOT_UNAVAILABLE", "Horario no disponible.");
        try
        {
            Execute(c, t, """
                UPDATE check_in_attempts SET active=false,state='Rejected',version=version+1,
                    ready_at=NULL,invalidated_at=now(),updated_at=now()
                WHERE appointment_id=@id AND active
                """, ("id", id.ToString()));
            Execute(c, t, """
                UPDATE appointments SET slot_id=@slot,practitioner_id=@p,start_utc=@start,end_utc=@end,
                status='PendingValidation',coverage='ManualReview',evidence=NULL,coverage_valid_until=NULL,
                reviewed_by=NULL,reviewed_at=NULL,version=version+1 WHERE id=@id
                """, ("slot", request.SlotId.ToString()), ("p", s["practitioner_id"]), ("start", s["start_utc"]),
                ("end", s["end_utc"]), ("id", id.ToString()));
        }
        catch (Npgsql.PostgresException e) when (e.SqlState is "23505" or "23P01")
        {
            return Fail(409, "SLOT_UNAVAILABLE", "El horario ya fue solicitado.");
        }
        notifications.AppointmentEvent(c, t, id.ToString(), "rescheduled");
        Log(c, t, "appointment.rescheduled", id.ToString()); t.Commit(); return Data(new { rescheduled = true });
    }

    private static List<Dictionary<string, object?>> AvailableSlots(Npgsql.NpgsqlConnection c,
        Npgsql.NpgsqlTransaction t, string target, string? previous = null, string appointment = "")
    {
        var slotIds = previous is null ? new[] { target } : new[] { previous, target };
        var identities = OfferPolicy.Identities(c, t, slotIds);
        var targetIdentity=identities.SingleOrDefault(row=>row.Id==target);
        if (targetIdentity is null) return [];
        OfferPolicy.LockOffers(c, t, identities);
        // Re-read target association/publication after waiting for offer locks, before slot locks.
        var offer=OfferPolicy.Read(c,t,targetIdentity.ServiceId,targetIdentity.PractitionerId);
        if(offer is null || !offer.ServiceActive || !offer.PractitionerActive || !offer.Published || !offer.Assigned) return [];
        OfferPolicy.LockSlots(c, t, slotIds);
        // This read has a fresh READ COMMITTED snapshot after all catalog locks.
        return Rows(c, t, """
            SELECT s.* FROM slots s JOIN services v ON v.id=s.service_id JOIN practitioners p ON p.id=s.practitioner_id
            WHERE s.id=@id AND NOT s.blocked AND s.start_utc>@nowUtc AND p.active AND
            """ + " " + OfferPolicy.PublicServiceSql + " " + """
             AND EXISTS(SELECT 1 FROM practitioner_services ps WHERE ps.practitioner_id=p.id AND ps.service_id=v.id AND ps.active)
            AND NOT EXISTS(SELECT 1 FROM appointments a WHERE a.slot_id=s.id AND a.id<>@appointment AND a.status IN ('PendingValidation','Confirmed'))
            """, ("id", target), ("appointment", appointment), ("nowUtc", DateTime.UtcNow));
    }

}
