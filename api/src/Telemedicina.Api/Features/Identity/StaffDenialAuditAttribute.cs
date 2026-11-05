using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;

namespace Telemedicina.Api.Features.Identity;

[AttributeUsage(AttributeTargets.Class)]
public sealed class StaffDenialAuditAttribute : Attribute, IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var status = context.Result switch
        {
            ObjectResult value => value.StatusCode ?? 200,
            StatusCodeResult value => value.StatusCode,
            _ => context.HttpContext.Response.StatusCode
        };
        if (status < 400 || context.HttpContext.User.Identity?.IsAuthenticated != true ||
            context.HttpContext.Items.ContainsKey("staff.denial.recorded")) return;
        using var c = context.HttpContext.RequestServices.GetRequiredService<Database>().Open();
        using var t = c.BeginTransaction();
        Record(c, t, UserId(context.HttpContext.User), "staff.request_denied", "staff",
            context.HttpContext.TraceIdentifier, "Denied");
        t.Commit();
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
}
