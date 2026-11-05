using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using Telemedicina.Api.Infrastructure.Authentication;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Common.Models.ApiResponses;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;

namespace Telemedicina.Api.Features.Identity;

[ProducesResponseType(typeof(ErrorApiResponse), 400)]
[ProducesResponseType(typeof(ErrorApiResponse), 401)]
[ProducesResponseType(typeof(ErrorApiResponse), 403)]
[ProducesResponseType(typeof(ErrorApiResponse), 404)]
[ProducesResponseType(typeof(ErrorApiResponse), 409)]
[ProducesResponseType(typeof(ErrorApiResponse), 429)]
[ApiController, StaffDenialAudit, Authorize(Roles = "Administrator"), Route("api/v1/admin/staff")]
public sealed class StaffController(Database database, IHostEnvironment environment,
    IConfiguration configuration) : ApiControllerBase(database)
{
    private const string Managed = "role IN ('Admission','Practitioner','Support')";
    private const string SelectAccount = """
        SELECT id,email,role,version,
        CASE WHEN NOT verified THEN 'PendingActivation' WHEN active THEN 'Active' ELSE 'Suspended' END AS status
        FROM users
        """;
    private static StaffAccount Account(Dictionary<string, object?> row) =>
        new((string)row["id"]!, (string)row["email"]!, (string)row["role"]!,
            (string)row["status"]!, (int)row["version"]!);

    private NpgsqlTransaction Authorized(NpgsqlConnection c, string permission, string? target = null)
    {
        var t = c.BeginTransaction();
        try
        {
            AccountGuard.LockUsers(c, t, target is null ? [UserId(User)] : [UserId(User), target]);
            AccountGuard.Require(c, t, User, permission);
            return t;
        }
        catch { t.Dispose(); throw; }
    }

    [HttpGet, ProducesResponseType(typeof(ApiResponse<StaffPage>), 200)]
    public IActionResult List([FromQuery] string? after = null, [FromQuery] int limit = 20,
        [FromQuery] string? role = null, [FromQuery] string? status = null)
    {
        if (limit is < 1 or > 100 || (after is not null && !Guid.TryParseExact(after, "D", out _)) ||
            (role is not null && !AccessCatalog.IsManagedStaff(role)) ||
            (status is not null && status is not ("PendingActivation" or "Active" or "Suspended")))
            return Fail(400, "VALIDATION_FAILED", "Filtros no válidos.");
        using var c = Database.Open(); using var t = Authorized(c, "staff.read");
        var rows = Rows(c, t, SelectAccount + " " + """
            WHERE role IN ('Admission','Practitioner','Support') AND id>@after
            AND (@role='' OR role=@role)
            AND (@status='' OR CASE WHEN NOT verified THEN 'PendingActivation'
                 WHEN active THEN 'Active' ELSE 'Suspended' END=@status)
            ORDER BY id LIMIT @limit
            """, ("after", after ?? ""), ("role", role ?? ""), ("status", status ?? ""), ("limit", limit + 1));
        var more = rows.Count > limit;
        var items = rows.Take(limit).Select(Account).ToArray();
        return Data(new StaffPage(items, more ? items[^1].Id : null));
    }

    [HttpPost("invitations"), EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccount>), 201)]
    public IActionResult Invite(InviteStaffRequest request)
    {
        StaffInvitations.RequireDevelopment(environment, configuration);
        using var c = Database.Open(); using var t = Authorized(c, "staff.invite");
        var id = Guid.NewGuid().ToString();
        var inserted = Execute(c, t, """
            INSERT INTO users(id,email,password_hash,role,active,verified,created_by)
            VALUES(@id,@email,@hash,@role,false,false,@actor) ON CONFLICT DO NOTHING
            """, ("id", id), ("email", request.Email.Trim().ToLowerInvariant()),
            ("hash", Password(NewToken())), ("role", request.Role), ("actor", UserId(User)));
        if (inserted == 0)
            return Denied(c, t, 409, "ACCOUNT_ALREADY_EXISTS", "staff.invite");
        StaffInvitations.Issue(c, t, id, UserId(User));
        Record(c, t, UserId(User), "staff.invited", id, HttpContext.TraceIdentifier);
        var account = Account(Rows(c, t, SelectAccount + " WHERE id=@id", ("id", id))[0]);
        t.Commit();
        return Data(account, 201);
    }

    [HttpPost("{id:guid}/invitation"), EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccount>), 200)]
    public IActionResult Reinvite(Guid id, StaffVersionRequest request) =>
        Mutate(id, "staff.invite", request.ExpectedVersion, "invitation", null, null);

    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccount>), 200)]
    public IActionResult Role(Guid id, StaffRoleRequest request) =>
        Mutate(id, "staff.role.change", request.ExpectedVersion, "role", request.Role, null);

    [HttpPatch("{id:guid}/active")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccount>), 200)]
    public IActionResult Active(Guid id, StaffActiveRequest request) =>
        Mutate(id, "staff.state.change", request.ExpectedVersion, "state", null, request.Active);

    [HttpPost("{id:guid}/revoke-sessions")]
    [ProducesResponseType(typeof(ApiResponse<StaffAccount>), 200)]
    public IActionResult RevokeSessions(Guid id, StaffVersionRequest request) =>
        Mutate(id, "staff.sessions.revoke", request.ExpectedVersion, "sessions", null, null);

    private IActionResult Mutate(Guid id, string permission, int version, string operation, string? role, bool? active)
    {
        if (operation == "invitation") StaffInvitations.RequireDevelopment(environment, configuration);
        using var c = Database.Open(); using var t = Authorized(c, permission, id.ToString());
        if (id.ToString() == UserId(User))
            return Denied(c, t, 403, "ACCESS_DENIED", "staff." + operation);
        var row = Rows(c, t, SelectAccount + " WHERE id=@id AND " + Managed, ("id", id.ToString())).FirstOrDefault();
        if (row is null) return Denied(c, t, 404, "NOT_FOUND", "staff." + operation);
        var account = Account(row);
        if (account.Version != version)
            return Denied(c, t, 409, "ACCOUNT_VERSION_CONFLICT", "staff." + operation);
        if (operation == "invitation")
        {
            if (account.Status != "PendingActivation")
                return Denied(c, t, 409, "ACCOUNT_TRANSITION_INVALID", "staff.invitation");
            var recent = Convert.ToInt64(Scalar(c, t,
                "SELECT count(*) FROM staff_invitations WHERE user_id=@u AND issued_at>@n",
                ("u", account.Id), ("n", Now - 60)));
            if (recent > 0) return Denied(c, t, 429, "RATE_LIMITED", "staff.invitation");
            StaffInvitations.Issue(c, t, account.Id, UserId(User));
        }
        else if (operation == "state")
        {
            if (account.Status == "PendingActivation" ||
                (account.Status == "Active") == active!.Value)
                return Denied(c, t, 409, "ACCOUNT_TRANSITION_INVALID", "staff.state");
            Execute(c, t, "UPDATE users SET active=@active WHERE id=@u",
                ("active", active.Value), ("u", account.Id));
        }
        else if (operation == "role")
        {
            if (role is null || !AccessCatalog.IsManagedStaff(role) || role == account.Role)
                return Denied(c, t, 409, "ACCOUNT_TRANSITION_INVALID", "staff.role");
            Execute(c, t, "UPDATE users SET role=@role WHERE id=@u", ("role", role), ("u", account.Id));
        }
        if (operation is "role" or "sessions" || (operation == "state" && active == false))
        {
            Execute(c, t, "UPDATE sessions SET revoked=true WHERE user_id=@u", ("u", account.Id));
            StaffInvitations.Revoke(c, t, account.Id);
        }
        Execute(c, t, "UPDATE users SET version=version+1 WHERE id=@u", ("u", account.Id));
        Record(c, t, UserId(User), "staff." + operation + "_changed", account.Id, HttpContext.TraceIdentifier);
        var updated = Account(Rows(c, t, SelectAccount + " WHERE id=@id", ("id", account.Id))[0]);
        t.Commit();
        return Data(updated);
    }

    private IActionResult Denied(NpgsqlConnection c, NpgsqlTransaction t, int status, string code, string action)
    {
        // Neutral resource: no confirmation that a forbidden/missing target exists.
        Record(c, t, UserId(User), action, "staff", HttpContext.TraceIdentifier, "Denied");
        HttpContext.Items["staff.denial.recorded"] = true;
        t.Commit();
        return Fail(status, code, "No se pudo completar la operación.");
    }

    [HttpGet("audit-events"), ProducesResponseType(typeof(ApiResponse<StaffAuditPage>), 200)]
    public IActionResult Audit([FromQuery] long after = 0, [FromQuery] int limit = 20)
    {
        if (after < 0 || limit is < 1 or > 100) return Fail(400, "VALIDATION_FAILED", "Filtros no válidos.");
        using var c = Database.Open(); using var t = Authorized(c, "staff.audit.read");
        var rows = Rows(c, t, """
            SELECT sequence,id,actor_id AS "actorId",action,resource_id AS "resourceId",
                occurred_at AS "occurredAt",result,trace_id AS "traceId"
            FROM audit WHERE sequence>@after AND action LIKE 'staff.%'
            ORDER BY sequence LIMIT @limit
            """, ("after", after), ("limit", limit + 1));
        var more = rows.Count > limit;
        var items = rows.Take(limit).ToArray();
        return Data(new StaffAuditPage(items, more ? (long)items[^1]["sequence"]! : null));
    }
}
