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

namespace Telemedicina.Api.Features.Identity;

[ApiController, Authorize, Route("api/v1")]
public sealed class AccountsController(Database database) : ApiControllerBase(database)
{
    [Authorize(Roles = "Admission"), HttpPatch("admin/users/{id:guid}/active")]
    public IActionResult Active(Guid id, ActiveRequest request)
    {
        if (id.ToString() == UserId(User)) return Fail(400, "SELF_SUSPENSION", "No puede suspender su propia cuenta.");
        using var c = Database.Open(); using var t = Begin(c);
        var count = Execute(c, t, "UPDATE users SET active=@active WHERE id=@id AND role='Patient'",
            ("active", request.Active), ("id", id.ToString()));
        if (count == 0) return NotFound();
        if (!request.Active) Execute(c, t, "UPDATE sessions SET revoked=true WHERE user_id=@id", ("id", id.ToString()));
        Log(c, t, "account.active_changed", id.ToString()); t.Commit(); return Data(new { active = request.Active });
    }
}
