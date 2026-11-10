using Npgsql;
using Telemedicina.Api.Features.Notifications;
using Telemedicina.Api.Infrastructure.Email;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;

namespace Telemedicina.Api.Features.Identity;

internal static class StaffInvitations
{
    internal const string Kind = "staff-activation";

    public static void RequireDevelopment(IHostEnvironment environment, IConfiguration config)
    {
        if (!environment.IsDevelopment() || !config.GetValue<bool>("Milestone:Enabled"))
            throw new InvalidOperationException("Synthetic accounts require explicit Development configuration.");
    }

    internal static string ActivationUrl(IConfiguration config)
    {
        var origin = config["Milestone:PortalOrigin"] ?? "http://localhost:3000";
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/" ||
            (uri.Scheme == "http" && !uri.IsLoopback))
            throw new InvalidOperationException("Invalid staff activation portal origin.");
        return uri.GetLeftPart(UriPartial.Authority) + "/activar-personal";
    }

    public static void Revoke(NpgsqlConnection c, NpgsqlTransaction t, string user)
    {
        // Callers hold the user row first. The worker takes only this advisory lock,
        // never a user-row lock, so invalidation and SMTP are serialized.
        Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", "mailuser:" + user));
        Execute(c, t, "UPDATE staff_invitations SET used=true WHERE user_id=@u AND NOT used", ("u", user));
        Execute(c, t, """
            UPDATE email_outbox SET
                state=CASE WHEN state='Unknown' THEN state ELSE 'Superseded' END,
                payload_cipher='',locked_until=NULL
            WHERE user_id=@u AND kind=@kind AND state IN ('Queued','Sending','Unknown')
            """, ("u", user), ("kind", Kind));
        Execute(c, t, "DELETE FROM dev_mailbox WHERE user_id=@u AND purpose='staff-activation'", ("u", user));
    }

    public static void Issue(NpgsqlConnection c, NpgsqlTransaction t, string user, string actor,
        NotificationService notifications, MailSettings mail, IConfiguration config)
    {
        var url = ActivationUrl(config);
        mail.Validate();
        Revoke(c, t, user);
        var token = NewToken();
        var now = Now;
        var expires = now + 900;
        var hash = Hash(token);
        Execute(c, t, """
            INSERT INTO staff_invitations(token_hash,user_id,expires,issued_at,created_by)
            VALUES(@h,@u,@e,@n,@actor)
            """, ("h", hash), ("u", user), ("e", expires), ("n", now), ("actor", actor));
        notifications.Queue(c, t, user, Kind, Kind + ":" + hash, "Activa tu cuenta de personal",
            "Has recibido una invitación para activar tu cuenta de personal.\n" +
            "Abre " + url + " y copia este código de invitación:\n" + token +
            "\nDefine tu contraseña en el portal. La invitación caduca en 15 minutos y se utiliza una sola vez." +
            "\nSi no esperabas esta invitación, puedes ignorar este mensaje.",
            expires: DateTimeOffset.FromUnixTimeSeconds(expires).UtcDateTime);
        // Development capture remains private. SMTP tokens exist only in the encrypted payload.
        if (mail.Mode == "development")
            Execute(c, t, """
                INSERT INTO dev_mailbox(user_id,purpose,token,expires)
                VALUES(@u,'staff-activation',@token,@e)
                ON CONFLICT(user_id,purpose) DO UPDATE SET token=excluded.token,expires=excluded.expires
                """, ("u", user), ("token", token), ("e", expires));
    }

    internal static string? DeliveryState(NpgsqlConnection c, Dictionary<string, object?> row)
    {
        // Match the exact invitation, not merely the newest invitation for this user.
        // Never use appointment resource/version fields to correlate identity mail.
        return Scalar(c, null, """
            SELECT CASE
                WHEN i.expires<=@now THEN 'Expired'
                WHEN i.used OR u.verified OR u.active OR u.role NOT IN ('Admission','Practitioner','Support')
                    THEN 'Superseded'
                ELSE 'Eligible' END
            FROM staff_invitations i JOIN users u ON u.id=i.user_id
            WHERE i.user_id=@u AND @key='staff-activation:' || i.token_hash
            """, ("u", row["user_id"]), ("key", row["event_key"]), ("now", Now)) switch
        {
            "Eligible" => null,
            "Expired" => "Expired",
            _ => "Superseded"
        };
    }
}
