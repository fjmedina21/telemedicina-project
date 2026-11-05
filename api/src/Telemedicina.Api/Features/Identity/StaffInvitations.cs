using Npgsql;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;

namespace Telemedicina.Api.Features.Identity;

internal static class StaffInvitations
{
    public static void RequireDevelopment(IHostEnvironment environment, IConfiguration config)
    {
        if (!environment.IsDevelopment() || !config.GetValue<bool>("Milestone:Enabled"))
            throw new InvalidOperationException("Synthetic accounts require explicit Development configuration.");
    }

    public static void Revoke(NpgsqlConnection c, NpgsqlTransaction t, string user)
    {
        Execute(c, t, "UPDATE staff_invitations SET used=true WHERE user_id=@u AND NOT used", ("u", user));
        Execute(c, t, "DELETE FROM dev_mailbox WHERE user_id=@u AND purpose='staff-activation'", ("u", user));
    }

    public static void Issue(NpgsqlConnection c, NpgsqlTransaction t, string user, string actor)
    {
        Revoke(c, t, user);
        var token = NewToken();
        var now = Now;
        Execute(c, t, """
            INSERT INTO staff_invitations(token_hash,user_id,expires,issued_at,created_by)
            VALUES(@h,@u,@e,@n,@actor)
            """, ("h", Hash(token)), ("u", user), ("e", now + 900), ("n", now), ("actor", actor));
        // Only the existing private, key-protected dev mailbox can retrieve this token.
        // No SMTP/outbox entry, token in HTTP responses, logging, or URL.
        Execute(c, t, """
            INSERT INTO dev_mailbox(user_id,purpose,token,expires)
            VALUES(@u,'staff-activation',@token,@e)
            ON CONFLICT(user_id,purpose) DO UPDATE SET token=excluded.token,expires=excluded.expires
            """, ("u", user), ("token", token), ("e", now + 900));
    }
}
