using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Common.Models.ApiResponses;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Infrastructure.Video;
using Telemedicina.Api.Shared;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Features.Video;

[ApiController, Authorize(Roles = "Patient,Practitioner"), Route("api/v1")]
public sealed class VideoController(Database database, LiveKitTokenService liveKit) : ApiControllerBase(database)
{
    private static readonly HashSet<string> EventTypes = ["connecting", "connected", "reconnecting", "reconnected", "disconnected", "failed"];

    [HttpPost("appointments/{id:guid}/video-token")]
    [ProducesResponseType(typeof(ApiResponse<VideoTokenResponse>), 200)]
    public IActionResult Token(Guid id)
    {
        if (!liveKit.IsConfigured) return Fail(503, "VIDEO_PROVIDER_NOT_CONFIGURED", "El proveedor de video no está configurado.");
        var key = Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParse(key, out _)) return Fail(400, "IDEMPOTENCY_REQUIRED", "Envíe Idempotency-Key como UUID.");
        using var c = Database.Open(); using var t = Begin(c);
        Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", $"video:{UserId(User)}:{id}"));
        var appointment = AuthorizedAppointment(c, t, id);
        if (appointment is null) return NotFound();
        var session = LockSession(c, t, id);
        if (session is not null && session["status"] as string is "Ended" or "Failed") session = null;
        if (session is null)
        {
            var sessionId = Guid.NewGuid().ToString();
            var room = $"telemedicina-{id:N}";
            Execute(c, t, "INSERT INTO video_sessions(id,appointment_id,provider,provider_room_id,status) VALUES(@id,@a,'livekit',@room,'Waiting')",
                ("id", sessionId), ("a", id.ToString()), ("room", room));
            session = LockSession(c, t, id);
            Log(c, t, "video.session.created", id.ToString());
        }
        var status = (string)session!["status"]!;
        var practitioner = User.IsInRole("Practitioner");
        if (practitioner && status != "Active") return Fail(409, "VIDEO_NOT_ADMITTED", "El profesional aún no ha admitido la sesión.");
        var payload = JsonSerializer.Serialize(new { sessionId = session["id"], role = practitioner ? "Practitioner" : "Patient" });
        var existing = Rows(c, t, "SELECT request_hash,response FROM idempotency WHERE user_id=@u AND key=@k", ("u", UserId(User)), ("k", key)).FirstOrDefault();
        if (existing is not null && (string)existing["request_hash"]! != Hash(payload)) return Fail(409, "IDEMPOTENCY_MISMATCH", "La clave ya se usó con otra solicitud.");
        if (existing is null) Execute(c, t, "INSERT INTO idempotency(user_id,key,request_hash,response) VALUES(@u,@k,@h,@r)",
            ("u", UserId(User)), ("k", key), ("h", Hash(payload)), ("r", payload));
        var token = liveKit.Issue(UserId(User), (string)session["provider_room_id"]!, User.Identity?.Name ?? UserId(User), true);
        var result = new VideoTokenResponse((string)session["id"]!, status, token.Token, token.Url, token.ExpiresAt);
        Log(c, t, "video.token.issued", id.ToString()); t.Commit(); return Data(result);
    }

    [HttpGet("appointments/{id:guid}/video-status")]
    [ProducesResponseType(typeof(ApiResponse<VideoStatusResponse>), 200)]
    public IActionResult Status(Guid id)
    {
        using var c = Database.Open(); using var t = Begin(c);
        if (AuthorizedAppointment(c, t, id) is null) return NotFound();
        var session = LockSession(c, t, id);
        var result = session is null
            ? new VideoStatusResponse("", "NotStarted", null, null, 0)
            : new VideoStatusResponse((string)session["id"]!, (string)session["status"]!,
                session["started_at"] as DateTime?, session["ended_at"] as DateTime?,
                Convert.ToInt32(session["connected_participants"] ?? 0));
        Log(c, t, "video.status.read", id.ToString()); t.Commit(); return Data(result);
    }

    [Authorize(Roles = "Practitioner"), HttpPost("video-sessions/{id:guid}/admit")]
    public IActionResult Admit(Guid id)
    {
        using var c = Database.Open(); using var t = Begin(c);
        var session = Rows(c, t, "SELECT vs.*,a.practitioner_id,p.user_id FROM video_sessions vs JOIN appointments a ON a.id=vs.appointment_id JOIN practitioners p ON p.id=a.practitioner_id WHERE vs.id=@id FOR UPDATE", ("id", id.ToString())).FirstOrDefault();
        if (session is null || ((string)session["practitioner_id"]! != UserId(User) && (string?)session["user_id"] != UserId(User))) return NotFound();
        if ((string)session["status"]! is "Ended" or "Failed") return Fail(409, "VIDEO_SESSION_CLOSED", "La sesión ya terminó.");
        Execute(c, t, "UPDATE video_sessions SET status='Active',started_at=coalesce(started_at,now()),updated_at=now() WHERE id=@id", ("id", id.ToString()));
        Log(c, t, "video.session.admitted", id.ToString()); t.Commit(); return Data(new { status = "Active" });
    }

    [Authorize(Roles = "Practitioner"), HttpPost("video-sessions/{id:guid}/end")]
    public IActionResult End(Guid id)
    {
        using var c = Database.Open(); using var t = Begin(c);
        var session = Rows(c, t, "SELECT vs.*,a.practitioner_id,p.user_id FROM video_sessions vs JOIN appointments a ON a.id=vs.appointment_id JOIN practitioners p ON p.id=a.practitioner_id WHERE vs.id=@id FOR UPDATE", ("id", id.ToString())).FirstOrDefault();
        if (session is null || ((string)session["practitioner_id"]! != UserId(User) && (string?)session["user_id"] != UserId(User))) return NotFound();
        Execute(c, t, "UPDATE video_sessions SET status='Ended',ended_at=coalesce(ended_at,now()),updated_at=now() WHERE id=@id AND status NOT IN ('Ended','Failed')", ("id", id.ToString()));
        Log(c, t, "video.session.ended", id.ToString()); t.Commit(); return Data(new { status = "Ended" });
    }

    [HttpPost("video-sessions/{id:guid}/connection-events")]
    public IActionResult ConnectionEvent(Guid id, VideoConnectionEventRequest request)
    {
        if (!EventTypes.Contains(request.Type)) return Fail(400, "VIDEO_EVENT_INVALID", "Evento de conexión no permitido.");
        using var c = Database.Open(); using var t = Begin(c);
        var session = Rows(c, t, "SELECT vs.id FROM video_sessions vs JOIN appointments a ON a.id=vs.appointment_id JOIN practitioners p ON p.id=a.practitioner_id WHERE vs.id=@id AND vs.status NOT IN ('Ended','Failed') AND (a.user_id=@u OR a.practitioner_id=@u OR p.user_id=@u)", ("id", id.ToString()), ("u", UserId(User))).FirstOrDefault();
        if (session is null) return NotFound();
        Execute(c, t, "INSERT INTO video_connection_events(id,session_id,actor_id,event_type) VALUES(@id,@s,@a,@e)",
            ("id", Guid.NewGuid().ToString()), ("s", id.ToString()), ("a", UserId(User)), ("e", request.Type));
        if (request.Type == "connected") Execute(c, t, "UPDATE video_sessions SET connected_participants=least(connected_participants+1,2),updated_at=now() WHERE id=@id", ("id", id.ToString()));
        if (request.Type == "disconnected") Execute(c, t, "UPDATE video_sessions SET connected_participants=greatest(connected_participants-1,0),updated_at=now() WHERE id=@id", ("id", id.ToString()));
        t.Commit(); return Data(new { accepted = true });
    }

    private Dictionary<string, object?>? AuthorizedAppointment(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, Guid id) =>
        Rows(c, t, "SELECT a.id,a.user_id,a.practitioner_id FROM appointments a JOIN practitioners p ON p.id=a.practitioner_id JOIN ready_check_ins r ON r.appointment_id=a.id WHERE a.id=@id AND (a.user_id=@u OR a.practitioner_id=@u OR p.user_id=@u)", ("id", id.ToString()), ("u", UserId(User))).FirstOrDefault();
    private static Dictionary<string, object?>? LockSession(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, Guid appointment) =>
        Rows(c, t, "SELECT * FROM video_sessions WHERE appointment_id=@a AND status NOT IN ('Ended','Failed') ORDER BY created_at DESC LIMIT 1 FOR UPDATE", ("a", appointment.ToString())).FirstOrDefault();
}
