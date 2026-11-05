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

namespace Telemedicina.Api.Features.Catalog;

[ApiController, Authorize, Route("api/v1")]
public sealed class CatalogReadController(Database database) : ApiControllerBase(database)
{
    [HttpGet("specialties")]
    public IActionResult Specialties()
    {
        using var c = Database.Open();
        return Data(Rows(c, null, "SELECT DISTINCT specialty AS name FROM services WHERE active ORDER BY specialty"));
    }
    [HttpGet("services")]
    public IActionResult Services([FromQuery] string? specialty = null)
    {
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT id,name,specialty,duration AS "durationMinutes" FROM services
            WHERE active AND (@s='' OR specialty=@s) ORDER BY name
            """, ("s", specialty ?? "")));
    }
    [HttpGet("practitioners")]
    public IActionResult Practitioners()
    {
        using var c = Database.Open();
        return Data(Rows(c, null, "SELECT id,name FROM practitioners WHERE active ORDER BY name"));
    }

}
