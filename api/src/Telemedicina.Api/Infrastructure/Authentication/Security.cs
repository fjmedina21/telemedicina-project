using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace Telemedicina.Api.Infrastructure.Authentication;

public static class Security
{
    public const string Scheme = "MilestoneSession";
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string NewVerificationCode()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Range(0, 6)
            .Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
    }
    public static string VerificationHash(string user, long expires, string code) =>
        Hash($"verify:{user}:{expires.ToString(System.Globalization.CultureInfo.InvariantCulture)}:{code.Trim().ToUpperInvariant()}");
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Password(string value) => new PasswordHasher<string>().HashPassword("account", value);
    public static bool CheckPassword(string hash, string value) =>
        new PasswordHasher<string>().VerifyHashedPassword("account", hash, value) != PasswordVerificationResult.Failed;
    public static string UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    // RFC 6238, six digits, 30-second steps. Used only for local synthetic staff accounts.
    public static long? TotpStep(string secret, string? code)
    {
        if (code is null || code.Length != 6 || code.Any(c => !char.IsAsciiDigit(c))) return null;
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var key = new List<byte>(); var bits = 0; var buffer = 0;
        foreach (var character in secret.TrimEnd('=').ToUpperInvariant())
        {
            var index = alphabet.IndexOf(character);
            if (index < 0) return null;
            buffer = (buffer << 5) | index; bits += 5;
            if (bits >= 8) { bits -= 8; key.Add((byte)(buffer >> bits)); }
        }
        if (key.Count < 20) return null;
        var current = Now / 30;
        for (var step = current - 1; step <= current + 1; step++)
        {
            var counter = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(step));
            var digest = HMACSHA1.HashData(key.ToArray(), counter);
            var offset = digest[^1] & 15;
            var number = ((digest[offset] & 127) << 24) | (digest[offset + 1] << 16) | (digest[offset + 2] << 8) | digest[offset + 3];
            var expected = Encoding.ASCII.GetBytes((number % 1000000).ToString("D6"));
            if (CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(code))) return step;
        }
        return null;
    }
}

