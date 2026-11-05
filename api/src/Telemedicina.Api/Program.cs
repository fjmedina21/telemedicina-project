using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Shared;
using Telemedicina.Api.Configuration;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Infrastructure.Persistence.Seeding;
using Telemedicina.Api.Infrastructure.Email;
using Telemedicina.Api.Infrastructure.Authentication;
using Telemedicina.Common.Models.ApiResponses;

var builder = WebApplication.CreateBuilder(args);
builder.AddTelemedicina();

// Add services to the container
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(ApiErrors.Create(context.HttpContext, 400, "VALIDATION_FAILED",
            "Los datos enviados no son válidos."));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Health checks
builder.Services.AddHealthChecks();

// CORS configuration for local development / multi-client access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalClients", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
            {
                return Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https") &&
                    (uri.Host == "localhost" || uri.Host == "127.0.0.1");
            })
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

if (args.Contains("--reset-demo-data", StringComparer.Ordinal))
{
    try
    {
        var options = DemoResetOptions.Parse(args);
        DemoDataReset.RequireEnvironment(app.Configuration, app.Environment);
        var report = DemoDataReset.Run(app.Services.GetRequiredService<Database>(),
            app.Configuration, app.Environment, options);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }
    catch (DemoResetException error) { Console.Error.WriteLine(error.Code); Environment.ExitCode = 1; }
    catch (Npgsql.PostgresException error) { Console.Error.WriteLine("DEMO_RESET_DATABASE_ERROR " + error.SqlState + " position=" + error.Position + " message=" + (error.SqlState == "42P08" ? error.MessageText : "details-suppressed")); Environment.ExitCode = 1; }
    catch (Exception error) { Console.Error.WriteLine("DEMO_RESET_FAILED " + error.GetType().Name); Environment.ExitCode = 1; }
    return;
}


if (args.Contains("--inspect") || args.Contains("--migrate") || args.Contains("--seed-synthetic") || args.Contains("--seed-staff-synthetic"))
{
    if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Milestone:Enabled"))
        throw new InvalidOperationException("Milestone commands require explicit development configuration.");
    var database = app.Services.GetRequiredService<Database>();
    if (args.Contains("--inspect"))
    {
        using var connection = database.Open();
        var metadata = Database.Rows(connection, null, """
            SELECT table_schema AS schema, count(*) AS tables FROM information_schema.tables
            WHERE table_schema NOT IN ('pg_catalog','information_schema') GROUP BY table_schema ORDER BY table_schema
            """);
        var extension = Database.Rows(connection, null, """
            SELECT name, installed_version FROM pg_available_extensions WHERE name='btree_gist'
            """);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { metadata, extension }));
    }
    if (args.Contains("--migrate"))
        database.Migrate(Path.Combine(app.Environment.ContentRootPath, "Infrastructure", "Persistence", "Migrations", "001_initial.sql"));
    if (args.Contains("--seed-synthetic"))
        SyntheticDataSeeder.Seed(database, app.Configuration, app.Environment);
    if (args.Contains("--seed-staff-synthetic"))
        StaffSyntheticSeeder.Seed(database, app.Configuration, app.Environment);
    return;
}

if (args.Contains("--check-smtp"))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("SMTP diagnostics are local-development only.");
    var transport = app.Services.GetRequiredService<MailTransport>();
    var connected = await transport.CheckAsync(CancellationToken.None);
    Console.WriteLine(transport.DiagnosticCode);
    if (transport.DiagnosticCode == "SMTP_TLS_FAILED")
        Console.WriteLine($"SMTP_TLS_POLICY={transport.TlsPolicyErrors}; SMTP_TLS_CHAIN={transport.TlsChainStatus}");
    Environment.ExitCode = connected ? 0 : 1;
    return;
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.RoutePrefix = "swagger";
        options.SwaggerEndpoint("/openapi/v1.json", "Telemedicina API v1");
    });
    app.UseCors("AllowLocalClients");
}

// Global exception handling with ProblemDetails / ErrorApiResponse
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        if (context.Features.Get<IExceptionHandlerFeature>()?.Error is SessionRejectedException rejected)
        {
            context.Response.StatusCode = rejected.Status;
            await context.Response.WriteAsJsonAsync(
                ApiErrors.Create(context, rejected.Status, rejected.Code, "Acceso no autorizado"),
                options: new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
                contentType: "application/problem+json");
            return;
        }
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        // Never include exception messages, submitted values or credentials in HTTP errors.
        var errorResponse = ApiErrors.Create(context, 500, "UNEXPECTED_ERROR",
            "No se pudo completar la solicitud.");

        await context.Response.WriteAsJsonAsync(errorResponse);
    });
});

app.UseHttpsRedirection();

// Liveness probe for infra / orchestrator
app.MapHealthChecks("/health");

// Supply the shared error model for bodyless framework, rate-limit and gate rejections.
app.UseStatusCodePages(async (StatusCodeContext statusContext) =>
{
    var context = statusContext.HttpContext;
    if (!context.Request.Path.StartsWithSegments("/api/v1")) return;
    var status = context.Response.StatusCode;
    var code = status switch
    {
        400 => "VALIDATION_FAILED", 401 => "SESSION_REQUIRED", 403 => "ACCESS_DENIED",
        404 => "NOT_FOUND", 405 => "METHOD_NOT_ALLOWED", 429 => "RATE_LIMITED", _ => "REQUEST_REJECTED"
    };
    await context.Response.WriteAsJsonAsync(
        ApiErrors.Create(context, status, code, "No se pudo completar la solicitud."),
        options: new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web),
        contentType: "application/problem+json");
});
app.UseTelemedicina();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
