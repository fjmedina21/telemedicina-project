using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Telemedicina.Api.Infrastructure.Video;

public sealed record LiveKitToken(string Token, string Url, DateTime ExpiresAt);

public sealed class LiveKitTokenService(IConfiguration configuration)
{
    private string? ApiKey => configuration["LiveKit:ApiKey"];
    private string? ApiSecret => configuration["LiveKit:ApiSecret"];
    public string? Url => configuration["LiveKit:Url"];
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(ApiSecret) && !string.IsNullOrWhiteSpace(Url);

    public LiveKitToken Issue(string identity, string room, string displayName, bool canPublish)
    {
        if (!IsConfigured) throw new InvalidOperationException("LiveKit is not configured.");
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddSeconds(Math.Clamp(configuration.GetValue("LiveKit:TokenTtlSeconds", 300), 60, 900));
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = ApiKey,
            sub = identity,
            name = displayName,
            nbf = now.AddSeconds(-5).ToUnixTimeSeconds(),
            exp = expires.ToUnixTimeSeconds(),
            jti = Guid.NewGuid().ToString("N"),
            video = new { roomJoin = true, room, canPublish, canSubscribe = true }
        }));
        var unsigned = Encoding.ASCII.GetBytes($"{header}.{payload}");
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(ApiSecret!), unsigned);
        return new($"{header}.{payload}.{Base64Url(signature)}", Url!, expires.UtcDateTime);
    }

    private static string Base64Url(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
