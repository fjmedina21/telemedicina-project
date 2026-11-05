using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Security.Cryptography;
using System.Text;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Infrastructure.Email;

public sealed class MailSettings(IConfiguration configuration, IWebHostEnvironment environment)
{
    public string Mode => configuration["Mail:Mode"] ?? "development";
    // Revocation remains mandatory outside local development. Certificate trust,
    // validity and hostname checks are never bypassed.
    public bool CheckCertificateRevocation => !environment.IsDevelopment()
        || configuration.GetValue("Mail:CheckCertificateRevocation", true);
    public string Host => configuration["Mail:Host"] ?? "smtp.gmail.com";
    public int Port => configuration.GetValue("Mail:Port", 587);
    public string Username => configuration["Mail:Username"] ?? "";
    public string Password => (configuration["Mail:Password"] ?? "").Replace(" ", "");
    public string Sender => configuration["Mail:Sender"] ?? Username;
    public bool IsLocalTest => environment.IsDevelopment() && configuration.GetValue<bool>("Mail:AllowLocalTest")
        && Host is "127.0.0.1" or "::1" or "localhost";
    public string Channel => Mode == "development" ? "development" : IsLocalTest ? "smtp-test" : "smtp";
    public int RetrySeconds => Math.Clamp(configuration.GetValue("Mail:RetrySeconds", 30), 1, 300);
    public byte[] PayloadKey => Convert.FromBase64String(configuration["Mail:PayloadKey"]
        ?? throw new InvalidOperationException("Configure Mail__PayloadKey outside source control."));
    public void Validate()
    {
        if (Mode is not ("development" or "smtp")) throw new InvalidOperationException("Unsupported mail mode.");
        if (Mode == "smtp")
        {
            if (!MailboxAddress.TryParse(Sender, out _) || (!IsLocalTest && (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))))
                throw new InvalidOperationException("SMTP configuration is incomplete.");
            if (PayloadKey.Length != 32) throw new InvalidOperationException("Mail__PayloadKey must encode 32 random bytes.");
            if (!IsLocalTest && Port is not (465 or 587)) throw new InvalidOperationException("SMTP requires TLS on port 465 or 587.");
        }
    }
    public string Protect(string body)
    {
        if (Mode == "development") return "";
        var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16];
        var plain = Encoding.UTF8.GetBytes(body); var cipher = new byte[plain.Length];
        using var aes = new AesGcm(PayloadKey, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return string.Join(".", Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(cipher));
    }
    public string Unprotect(string value)
    {
        var parts = value.Split('.');
        var cipher = Convert.FromBase64String(parts[2]); var plain = new byte[cipher.Length];
        using var aes = new AesGcm(PayloadKey, 16);
        aes.Decrypt(Convert.FromBase64String(parts[0]), cipher, Convert.FromBase64String(parts[1]), plain);
        return Encoding.UTF8.GetString(plain);
    }
}
