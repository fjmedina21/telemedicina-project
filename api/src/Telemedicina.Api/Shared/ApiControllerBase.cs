using System.Text.Json;
using Telemedicina.Common.Models.ApiResponses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Api.Shared.Contracts;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;
namespace Telemedicina.Api.Shared;

public abstract class ApiControllerBase(Database database) : ControllerBase
{
    protected Database Database { get; } = database;
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    protected IActionResult Data(object? data, int status = 200) =>
        StatusCode(status, new ApiResponse<object?>(status, "OK", data));
    protected IActionResult Fail(int status, string code, string message) =>
        StatusCode(status, ApiErrors.Create(HttpContext, status, code, message));
    protected Npgsql.NpgsqlTransaction Begin(Npgsql.NpgsqlConnection c)
    {
        var t = c.BeginTransaction();
        Execute(c, t, "SELECT set_config('telemedicina.actor',@actor,true)", ("actor", UserId(User)));
        return t;
    }
    protected void Log(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t, string action, string id) =>
        Record(c, t, UserId(User), action, id, HttpContext.TraceIdentifier);


}
