using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Security.Cryptography;
using System.Text;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Infrastructure.Email;

public sealed class MailTransport(MailSettings settings)
{
    public string DiagnosticCode { get; private set; } = "SMTP_NOT_CHECKED";
    public string TlsPolicyErrors { get; private set; } = "NotEvaluated";
    public string TlsChainStatus { get; private set; } = "NotEvaluated";
    private async Task Connect(SmtpClient client, CancellationToken cancellation)
    {
        client.Timeout = 15000;
        client.CheckCertificateRevocation = settings.CheckCertificateRevocation;
        await client.ConnectAsync(settings.Host, settings.Port, settings.IsLocalTest ? SecureSocketOptions.None :
            settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellation);
        if (!settings.IsLocalTest || !string.IsNullOrEmpty(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellation);
    }
    // Authentication check only: never issues SMTP MAIL FROM, RCPT TO or DATA.
    public async Task<bool> CheckAsync(CancellationToken cancellation)
    {
        try
        {
            settings.Validate();
            using var client = new SmtpClient();
            client.ServerCertificateValidationCallback = (_, _, chain, errors) =>
            {
                TlsPolicyErrors = errors.ToString();
                TlsChainStatus = string.Join(",", chain?.ChainStatus.Select(item => item.Status.ToString()) ?? []);
                return errors == System.Net.Security.SslPolicyErrors.None;
            };
            await Connect(client, cancellation);
            await client.NoOpAsync(cancellation);
            await client.DisconnectAsync(true, cancellation);
            DiagnosticCode = "SMTP_AUTH_OK";
            return true;
        }
        catch (MailKit.Security.AuthenticationException) { DiagnosticCode = "SMTP_AUTH_REJECTED"; }
        catch (MailKit.Security.SslHandshakeException) { DiagnosticCode = "SMTP_TLS_FAILED"; }
        catch (System.Net.Sockets.SocketException) { DiagnosticCode = "SMTP_UNREACHABLE"; }
        catch (OperationCanceledException) { DiagnosticCode = "SMTP_TIMEOUT"; }
        catch (TimeoutException) { DiagnosticCode = "SMTP_TIMEOUT"; }
        catch (IOException) { DiagnosticCode = "SMTP_IO_FAILED"; }
        catch (InvalidOperationException) { DiagnosticCode = "SMTP_CONFIGURATION_INVALID"; }
        catch (Exception) { DiagnosticCode = "SMTP_CHECK_FAILED"; }
        return false;
    }
    public async Task<(string State, string? Code)> SendAsync(Dictionary<string, object?> item, CancellationToken cancellation)
    {
        if (settings.Mode == "development") return ("Captured", null);
        if (!settings.IsLocalTest && ((string)item["recipient"]!).EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
            return ("Failed", "RESERVED_TEST_DOMAIN");
        var sendStarted = false;
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(settings.Sender));
            message.To.Add(MailboxAddress.Parse((string)item["recipient"]!));
            message.Subject = (string)item["subject"]!;
            message.MessageId = (string)item["id"]! + "@" + settings.Sender.Split('@')[1];
            message.Body = new TextPart("plain") { Text = settings.Unprotect((string)item["payload_cipher"]!) };
            using var client = new SmtpClient();
            await Connect(client, cancellation);
            sendStarted = true;
            await client.SendAsync(message, cancellation);
            // DATA was acknowledged. A disconnect failure must not retry an accepted message.
            try { await client.DisconnectAsync(true, cancellation); } catch { }
            return ("Sent", null);
        }
        catch (MailKit.Net.Smtp.SmtpCommandException e)
        {
            return ((int)e.StatusCode >= 500 ? "Failed" : "Queued", "SMTP_" + (int)e.StatusCode);
        }
        catch (MailKit.Security.AuthenticationException) { return ("Failed", "SMTP_AUTH_FAILED"); }
        catch (CryptographicException) { return ("Failed", "PAYLOAD_KEY_MISMATCH"); }
        catch (Exception) { return (sendStarted ? "Unknown" : "Queued", sendStarted ? "SMTP_ACK_UNKNOWN" : "SMTP_CONNECT_FAILED"); }
    }
}
