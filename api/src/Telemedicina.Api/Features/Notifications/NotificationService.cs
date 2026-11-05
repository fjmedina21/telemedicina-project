using Npgsql;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

using Telemedicina.Api.Infrastructure.Email;
using Telemedicina.Api.Infrastructure.Persistence;

namespace Telemedicina.Api.Features.Notifications;

public sealed class NotificationService(MailSettings mail)
{
    public void Queue(NpgsqlConnection c, NpgsqlTransaction t, string user, string kind, string eventKey, string subject, string body,
        string? resource = null, int? version = null, DateTime? expires = null, string? notification = null)
    {
        var recipient = Scalar(c, t, "SELECT email FROM users WHERE id=@id", ("id", user)) as string;
        if (recipient is null) return;
        Execute(c, t, """
            INSERT INTO email_outbox(id,event_key,notification_id,user_id,recipient,kind,subject,payload_cipher,
                resource_id,resource_version,expires_at,channel)
            VALUES(@id,@key,@notification,@u,@recipient,@kind,@subject,@body,@resource,@version,@expires,@channel)
            ON CONFLICT(event_key) DO NOTHING
            """, ("id", Guid.NewGuid().ToString()), ("key", eventKey), ("notification", notification), ("u", user),
            ("recipient", recipient), ("kind", kind), ("subject", subject), ("body", mail.Protect(body)),
            ("resource", resource), ("version", version), ("expires", expires), ("channel", mail.Channel));
    }

    public void AppointmentEvent(NpgsqlConnection c, NpgsqlTransaction t, string id, string kind)
    {
        Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", "delivery:" + id));
        var row = Rows(c, t, "SELECT user_id,version FROM appointments WHERE id=@id", ("id", id)).Single();
        var user = (string)row["user_id"]!; var version = (int)row["version"]!;
        var text = kind switch
        {
            "requested" => "Tu solicitud fue registrada. Inicia sesión para consultar su estado.",
            "cancelled" => "Tu cita fue cancelada. Inicia sesión para consultar los detalles.",
            "rescheduled" => "Tu cita cambió de horario y requiere revisión. Inicia sesión para consultar su estado.",
            _ => "El estado de tu cita cambió. Inicia sesión para consultar los detalles."
        };
        var key = "appointment:" + id + ":" + version + ":" + kind;
        var notification = Guid.NewGuid().ToString();
        var inserted = Execute(c, t, """
            INSERT INTO notifications(id,user_id,event_key,kind,body,resource_id,resource_version)
            VALUES(@id,@u,@key,@kind,@body,@resource,@version) ON CONFLICT(event_key) DO NOTHING
            """, ("id", notification), ("u", user), ("key", key), ("kind", kind), ("body", text), ("resource", id), ("version", version));
        if (inserted == 0) return;
        Execute(c, t, """
            UPDATE email_outbox SET state='Superseded',payload_cipher='' WHERE resource_id=@id AND state='Queued'
            """, ("id", id));
        var optedIn = Scalar(c, t, "SELECT email_enabled FROM notification_preferences WHERE user_id=@u", ("u", user));
        if (optedIn is true) Queue(c, t, user, kind, key, "Actualización de tu cita", text, id, version, notification: notification);
    }
}
