using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Security.Cryptography;
using System.Text;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Infrastructure.Email;

public sealed class MailWorker(IServiceProvider services, IConfiguration configuration, IWebHostEnvironment environment,
    ILogger<MailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellation)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Milestone:Enabled")) return;
        var settings = services.GetRequiredService<MailSettings>(); settings.Validate();
        while (!cancellation.IsCancellationRequested)
        {
            try { await DeliverOne(settings, cancellation); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Email worker unavailable; retrying without logging message content or credentials."); }
            try { await Task.Delay(TimeSpan.FromSeconds(1), cancellation); }
            catch (OperationCanceledException) { break; }
        }
    }
    private async Task DeliverOne(MailSettings settings, CancellationToken cancellation)
    {
        var database = services.GetRequiredService<Database>();
        using var c = database.Open();
        using (var t = c.BeginTransaction())
        {
            // A crashed send might have been accepted. Never blindly re-send its lease.
            Execute(c, t, """
                UPDATE email_outbox SET state='Unknown',last_error_code='LEASE_ACK_UNKNOWN',locked_until=NULL
                WHERE channel=@channel AND state='Sending' AND locked_until<now()
                """, ("channel", settings.Channel));
            t.Commit();
        }
        Dictionary<string, object?>? row;
        var lease = Guid.NewGuid().ToString();
        using (var t = c.BeginTransaction())
        {
            row = Rows(c, t, """
                SELECT * FROM email_outbox WHERE channel=@channel AND state='Queued' AND available_at<=now()
                ORDER BY created_at FOR UPDATE SKIP LOCKED LIMIT 1
                """, ("channel", settings.Channel)).FirstOrDefault();
            if (row is null) return;
            Execute(c, t, """
                UPDATE email_outbox SET state='Sending',attempts=attempts+1,lease_token=@lease,
                locked_until=now()+interval '2 minutes' WHERE id=@id
                """, ("lease", lease), ("id", row["id"]));
            t.Commit();
        }
        var resource = row["resource_id"] as string;
        var userKey = "mailuser:" + (string)row["user_id"]!;
        Execute(c, null, "SELECT pg_advisory_lock(hashtextextended(@key,0))", ("key", userKey));
        if (resource is not null) Execute(c, null, "SELECT pg_advisory_lock(hashtextextended(@key,0))", ("key", "delivery:" + resource));
        try
        {
            (string State, string? Code) result;
            if (row["expires_at"] is DateTime expiry && expiry <= DateTime.UtcNow) result = ("Expired", null);
            else if (row["resource_version"] is int version && resource is not null &&
                Convert.ToInt32(Scalar(c, null, "SELECT version FROM appointments WHERE id=@id", ("id", resource))) != version)
                result = ("Superseded", null);
            else if (row["notification_id"] is not null && Scalar(c, null, "SELECT email_enabled FROM notification_preferences WHERE user_id=@u", ("u", row["user_id"])) is not true)
                result = ("Superseded", null);
            else if (!Convert.ToBoolean(Scalar(c, null, "SELECT active FROM users WHERE id=@id", ("id", row["user_id"]))))
                result = ("Superseded", null);
            else
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(25));
                result = await services.GetRequiredService<MailTransport>().SendAsync(row, deadline.Token);
            }
            var attempts = (int)row["attempts"]! + 1;
            if (result.State == "Queued" && attempts >= 6) result = ("Failed", "RETRY_LIMIT");
            var delay = Math.Min(3600, settings.RetrySeconds * (1 << Math.Min(attempts - 1, 6)));
            Execute(c, null, """
                UPDATE email_outbox SET state=@state,last_error_code=@code,locked_until=NULL,
                available_at=now()+make_interval(secs=>@delay),
                sent_at=CASE WHEN @state IN ('Sent','Captured') THEN now() ELSE sent_at END,
                payload_cipher=CASE WHEN @state IN ('Sent','Captured','Superseded','Expired','Failed') THEN '' ELSE payload_cipher END
                WHERE id=@id AND lease_token=@lease
                """, ("state", result.State), ("code", result.Code), ("delay", delay), ("id", row["id"]), ("lease", lease));
        }
        finally
        {
            if (resource is not null) Execute(c, null, "SELECT pg_advisory_unlock(hashtextextended(@key,0))", ("key", "delivery:" + resource));
            Execute(c, null, "SELECT pg_advisory_unlock(hashtextextended(@key,0))", ("key", userKey));
        }
    }
}
