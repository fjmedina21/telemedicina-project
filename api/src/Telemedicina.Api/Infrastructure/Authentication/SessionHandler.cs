using System.Security.Claims;
using Telemedicina.Api.Shared;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Infrastructure.Authentication;

public sealed class SessionHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IServiceProvider services) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] :
            Request.Cookies["tm_access"];
        if (string.IsNullOrEmpty(token) || token.Length > 128) return Task.FromResult(AuthenticateResult.NoResult());
        using var c = services.GetRequiredService<Database>().Open();
        var rows = Rows(c, null, """
            SELECT u.id,u.email,u.role,s.id AS session_id FROM sessions s JOIN users u ON u.id=s.user_id
            WHERE s.access_hash=@hash AND NOT s.revoked AND s.access_expires>@now
            AND s.expires>@now AND u.active AND u.verified
            """, ("hash", Security.Hash(token)), ("now", Security.Now));
        if (rows.Count == 0) return Task.FromResult(AuthenticateResult.Fail("Session expired or revoked."));
        var row = rows[0];
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier,(string)row["id"]!),
            new Claim(ClaimTypes.Name,(string)row["email"]!), new Claim(ClaimTypes.Role,(string)row["role"]!),
            new Claim("session_id",(string)row["session_id"]!) };
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, Security.Scheme)), Security.Scheme)));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Reject(401, "SESSION_REQUIRED");
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Reject(403, "ACCESS_DENIED");
    private Task Reject(int status, string code)
    {
        Response.StatusCode = status;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(ApiErrors.Create(Context, status, code, "Acceso no autorizado"),
            options: new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
            contentType: "application/problem+json");
    }
}
