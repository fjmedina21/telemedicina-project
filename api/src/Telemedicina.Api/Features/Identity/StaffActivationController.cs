using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Telemedicina.Api.Infrastructure.Authentication;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;

namespace Telemedicina.Api.Features.Identity;

[ApiController, Route("api/v1/auth/staff"), EnableRateLimiting("auth")]
public sealed class StaffActivationController(Database database, IHostEnvironment environment,
    IConfiguration configuration) : ApiControllerBase(database)
{
    [HttpPost("activate"), ProducesResponseType(typeof(Telemedicina.Common.Models.ApiResponses.ApiResponse<StaffActivationResult>), 200)]
    public IActionResult Activate(ActivateStaffRequest request)
    {
        StaffInvitations.RequireDevelopment(environment, configuration);
        using var c = Database.Open(); using var t = c.BeginTransaction();
        var hash = Hash(request.Token.ToUpperInvariant());
        var user = Scalar(c, t,
            "SELECT user_id FROM staff_invitations WHERE token_hash=@h", ("h", hash)) as string;
        if (user is null) return Fail(400, "INVALID_CHALLENGE", "Invitación no válida.");
        AccountGuard.LockUsers(c, t, user);
        var row = Rows(c, t, """
            SELECT i.token_hash,u.role FROM staff_invitations i JOIN users u ON u.id=i.user_id
            WHERE i.token_hash=@h AND NOT i.used AND i.expires>@now AND NOT u.verified
            AND NOT u.active AND u.role IN ('Admission','Practitioner','Support')
            FOR UPDATE OF i
            """, ("h", hash), ("now", Now)).FirstOrDefault();
        if (row is null) return Fail(400, "INVALID_CHALLENGE", "Invitación no válida.");
        Execute(c, t, """
            UPDATE users SET password_hash=@hash,verified=true,active=true,failed_logins=0,
            locked_until=0,version=version+1 WHERE id=@u
            """, ("hash", Password(request.Password)), ("u", user));
        StaffInvitations.Revoke(c, t, user);
        Record(c, t, user, "staff.activated", user, HttpContext.TraceIdentifier);
        t.Commit();
        return Data(new StaffActivationResult(true));
    }
}
