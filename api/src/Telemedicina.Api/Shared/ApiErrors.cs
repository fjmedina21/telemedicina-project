using Telemedicina.Common.Models.ApiResponses;

namespace Telemedicina.Api.Shared;

public static class ApiErrors
{
    public static ErrorApiResponse Create(HttpContext context, int status, string code, string title) =>
        new(status, title, context.Request.Path.Value ?? "/")
        {
            Code = code,
            TraceId = context.TraceIdentifier
        };
}
