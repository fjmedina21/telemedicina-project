using System.Security.Claims;
using Npgsql;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Infrastructure.Authentication;

public sealed class SessionRejectedException(int status, string code) : Exception
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

/// <summary>
/// A protected write must lock its actor before any sessions/resources and revalidate.
/// Administrative actor/target locks are acquired in ordinal ID order.
/// Login, refresh, logout and staff activation use the same user-before-session order.
/// </summary>
public static class AccountGuard
{
    public static void LockUsers(NpgsqlConnection c, NpgsqlTransaction t, params string[] ids)
    {
        foreach (var id in ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            Scalar(c, t, "SELECT id FROM users WHERE id=@id FOR UPDATE", ("id", id));
    }

    public static string Require(NpgsqlConnection c, NpgsqlTransaction t, ClaimsPrincipal principal,
        string? permission = null)
    {
        var actor = Security.UserId(principal);
        // The caller holds the user row before checking the session.
        var role = Scalar(c, t, """
            SELECT u.role FROM users u JOIN sessions s ON s.user_id=u.id
            WHERE u.id=@u AND s.id=@s AND NOT s.revoked AND s.access_expires>@now
            AND s.expires>@now AND u.active AND u.verified
            """, ("u", actor), ("s", principal.FindFirstValue("session_id")), ("now", Security.Now)) as string;
        if (role is null) throw new SessionRejectedException(401, "SESSION_REQUIRED");
        if (role != principal.FindFirstValue(ClaimTypes.Role) ||
            (permission is not null && !AccessCatalog.Allows(role, permission)))
            throw new SessionRejectedException(403, "ACCESS_DENIED");
        return role;
    }
}
