using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Infrastructure.Authentication;
using Telemedicina.Api.Infrastructure.Email;
using Telemedicina.Api.Features.Notifications;

namespace Telemedicina.Api.Configuration;

public static class ApplicationExtensions
{
    public static void AddTelemedicina(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<Database>();
        builder.Services.AddSingleton<MailSettings>();
        builder.Services.AddSingleton<MailTransport>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddHostedService<MailWorker>();
        builder.Services.AddAuthentication(Security.Scheme)
            .AddScheme<AuthenticationSchemeOptions, SessionHandler>(Security.Scheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
    public static void UseTelemedicina(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/v1") &&
                !context.Request.Path.Equals("/api/v1/health", StringComparison.OrdinalIgnoreCase))
            {
                if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Milestone:Enabled"))
                {
                    context.Response.StatusCode = 404; return;
                }
                context.Response.Headers.CacheControl = "no-store";
                var origin = context.Request.Headers.Origin.ToString();
                var allowed = app.Configuration["Milestone:PortalOrigin"] ?? "http://localhost:3000";
                if (!string.IsNullOrEmpty(origin) && origin != allowed)
                {
                    context.Response.StatusCode = 403; return;
                }
                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method)
                    && context.Request.Headers["X-Telemedicina-Client"] != "milestone")
                {
                    context.Response.StatusCode = 403; return;
                }
            }
            await next(context);
        });
        app.UseRateLimiter();
        app.UseAuthentication();
    }


}
