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

namespace Telemedicina.Api.Features.Audit;

[ApiController, Authorize, Route("api/v1")]
public sealed class AuditController(Database database) : ApiControllerBase(database)
{
    [Authorize(Roles = "Admission"), HttpGet("admin/audit-events")]
    public IActionResult AuditEvents([FromQuery] int after = 0)
    {
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT sequence,id,actor_id AS "actorId",action,resource_id AS "resourceId",
            occurred_at AS "occurredAt",trace_id AS "traceId" FROM audit
            WHERE sequence>@after ORDER BY sequence LIMIT 100
            """, ("after", after)));
    }

}
