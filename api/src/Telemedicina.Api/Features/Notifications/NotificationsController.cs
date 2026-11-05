using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Api.Shared.Contracts;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;

namespace Telemedicina.Api.Features.Notifications;

[ApiController, Authorize, Route("api/v1")]
public sealed class NotificationsController(Database database) : ApiControllerBase(database)
{
    [Authorize(Roles = "Patient"), HttpGet("notifications/me")]
    public IActionResult Mine([FromQuery] int offset = 0)
    {
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT id,kind,body,resource_id AS "resourceId",created_at AS "createdAt",read_at AS "readAt"
            FROM notifications WHERE user_id=@u ORDER BY created_at DESC,id LIMIT 100 OFFSET @offset
            """, ("u", UserId(User)), ("offset", Math.Clamp(offset, 0, 10000))));
    }
    [Authorize(Roles = "Patient"), HttpPost("notifications/{id:guid}/read")]
    public IActionResult Read(Guid id)
    {
        using var c = Database.Open(); using var t = Begin(c);
        var count = Execute(c, t, "UPDATE notifications SET read_at=coalesce(read_at,now()) WHERE id=@id AND user_id=@u",
            ("id", id.ToString()), ("u", UserId(User)));
        if (count == 0) return NotFound();
        t.Commit();
        return Data(new { read = true });
    }
    [Authorize(Roles = "Patient"), HttpGet("notifications/preferences")]
    public IActionResult Preferences()
    {
        using var c = Database.Open();
        var row = Rows(c, null, "SELECT email_enabled AS \"emailEnabled\",version FROM notification_preferences WHERE user_id=@u",
            ("u", UserId(User))).FirstOrDefault();
        return Data(row is null ? new { emailEnabled = false, version = 0 } : (object)row);
    }
    [Authorize(Roles = "Patient"), HttpPatch("notifications/preferences")]
    public IActionResult SavePreferences(NotificationPreferencesRequest request)
    {
        using var c = Database.Open(); using var t = Begin(c);
        Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", "mailuser:" + UserId(User)));
        var old = Scalar(c, t, "SELECT version FROM notification_preferences WHERE user_id=@u FOR UPDATE", ("u", UserId(User)));
        if ((old is null ? 0 : (int)old) != request.Version)
            return Conflict(new { status = 409, title = "Las preferencias cambiaron. Actualice antes de guardar.", code = "VERSION_CONFLICT" });
        Execute(c, t, """
            INSERT INTO notification_preferences(user_id,email_enabled) VALUES(@u,@enabled)
            ON CONFLICT(user_id) DO UPDATE SET email_enabled=excluded.email_enabled,version=notification_preferences.version+1
            """, ("u", UserId(User)), ("enabled", request.EmailEnabled));
        if (!request.EmailEnabled)
            Execute(c, t, """
                UPDATE email_outbox SET state='Superseded',payload_cipher=''
                WHERE user_id=@u AND notification_id IS NOT NULL AND state='Queued'
                """, ("u", UserId(User)));
        Record(c, t, UserId(User), "notifications.preferences_changed", UserId(User), HttpContext.TraceIdentifier);
        t.Commit(); return Data(new { emailEnabled = request.EmailEnabled, version = request.Version + 1 });
    }
    [Authorize(Roles = "Admission"), HttpGet("admin/email-delivery")]
    public IActionResult Delivery()
    {
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT id,kind,state,attempts,available_at AS "availableAt",sent_at AS "sentAt",
            last_error_code AS "lastErrorCode",resource_id AS "resourceId"
            FROM email_outbox ORDER BY created_at DESC LIMIT 100
            """));
    }
}
