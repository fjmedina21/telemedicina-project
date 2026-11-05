using System.Text.Json;
using Telemedicina.Common.Models.ApiResponses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Api.Shared.Contracts;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;
using Npgsql;
using Microsoft.AspNetCore.RateLimiting;
using Telemedicina.Api.Features.Notifications;
using Telemedicina.Api.Infrastructure.Email;

namespace Telemedicina.Api.Features.Identity;

[ApiController, Route("api/v1/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(Database database, IConfiguration configuration, NotificationService notifications, MailSettings mail) : ApiControllerBase(database)
{
    private IActionResult Failure(int status, string code) =>
        Fail(status, code, "No se pudo completar la operación");
    private static readonly string DummyHash = Password(NewToken());

    [HttpPost("register")]
    public IActionResult Register(RegisterRequest request)
    {
        using var c = Database.Open(); using var t = c.BeginTransaction();
        var id = Guid.NewGuid().ToString();
        var inserted = Execute(c, t, """
            INSERT INTO users(id,email,password_hash,role) VALUES(@id,@email,@hash,'Patient')
            ON CONFLICT DO NOTHING
            """, ("id", id), ("email", request.Email.Trim().ToLowerInvariant()), ("hash", Password(request.Password)));
        if (inserted > 0)
        {
            Challenge(c, t, id, "verify"); Record(c, t, id, "account.registered", id, HttpContext.TraceIdentifier);
        }
        t.Commit();
        return Accepted(new ApiResponse<object?>(StatusCodes.Status202Accepted, "Si corresponde, revise el mensaje de verificación.", null));
    }
    private void Challenge(NpgsqlConnection c, NpgsqlTransaction t, string user, string purpose)
    {
        var token = purpose == "verify" ? NewVerificationCode() : NewToken(); var expires = Now + 900;
        var hash = purpose == "verify" ? VerificationHash(user, expires, token) : Hash(token);
        Execute(c, t, "UPDATE challenges SET used=true WHERE user_id=@u AND purpose=@p",
            ("u", user), ("p", purpose));
        Execute(c, t, "INSERT INTO challenges(token_hash,user_id,purpose,expires) VALUES(@h,@u,@p,@e)",
            ("h", hash), ("u", user), ("p", purpose), ("e", expires));
        Execute(c, t, "UPDATE email_outbox SET state='Superseded',payload_cipher='' WHERE user_id=@u AND kind=@kind AND state='Queued'",
            ("u", user), ("kind", "auth:" + purpose));
        notifications.Queue(c, t, user, "auth:" + purpose, "auth:" + hash,
            purpose == "verify" ? "Verifica tu cuenta" : "Recuperación de acceso",
            "Tu código de " + (purpose == "verify" ? "verificación" : "recuperación") + " es: " + token +
            "\nCaduca en 15 minutos. Si no lo solicitaste, puedes ignorar este mensaje.",
            expires: DateTime.UtcNow.AddMinutes(15));
        if (mail.Mode == "development") Execute(c, t, """
            INSERT INTO dev_mailbox(user_id,purpose,token,expires) VALUES(@u,@p,@token,@e)
            ON CONFLICT(user_id,purpose) DO UPDATE SET token=excluded.token,expires=excluded.expires
            """, ("u", user), ("p", purpose), ("token", token), ("e", expires));
    }
    [HttpPost("verification/resend")]
    public IActionResult Resend(ForgotRequest request)
    {
        using var c = Database.Open(); using var t = c.BeginTransaction();
        var user = Scalar(c, t, "SELECT id FROM users WHERE lower(email)=lower(@e) AND active AND NOT verified FOR UPDATE",
            ("e", request.Email.Trim())) as string;
        if (user is not null)
        {
            var recent = Convert.ToInt64(Scalar(c, t, "SELECT count(*) FROM challenges WHERE user_id=@u AND purpose='verify' AND expires>@recent",
                ("u", user), ("recent", Now + 840))) > 0;
            if (!recent) Challenge(c, t, user, "verify");
        }
        t.Commit();
        return Accepted(new ApiResponse<object?>(StatusCodes.Status202Accepted, "Si corresponde, revisa tu correo.", null));
    }
    [HttpPost("verify")]
    public IActionResult Verify(VerificationRequest request)
    {
        using var c = Database.Open(); using var t = c.BeginTransaction();
        // Serialize verification and resend per account; attempts survive process restarts.
        var user = Scalar(c, t, """
            SELECT id FROM users WHERE lower(email)=lower(@e) AND active AND NOT verified FOR UPDATE
            """, ("e", request.Email.Trim())) as string;
        if (user is null) return Failure(400, "INVALID_CHALLENGE");
        var challenge = Rows(c, t, """
            SELECT token_hash,expires,attempts FROM challenges
            WHERE user_id=@u AND purpose='verify' AND NOT used AND expires>@now
            ORDER BY expires DESC LIMIT 1 FOR UPDATE
            """, ("u", user), ("now", Now)).FirstOrDefault();
        if (challenge is null) return Failure(400, "INVALID_CHALLENGE");
        var stored = (string)challenge["token_hash"]!;
        var supplied = VerificationHash(user, (long)challenge["expires"]!, request.Token);
        var matches = System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(stored), System.Text.Encoding.ASCII.GetBytes(supplied));
        if ((int)challenge["attempts"]! >= 5 || !matches)
        {
            Execute(c, t, """
                UPDATE challenges SET attempts=attempts+1,used=(attempts+1>=5) WHERE token_hash=@h
                """, ("h", stored));
            t.Commit();
            return Failure(400, "INVALID_CHALLENGE");
        }
        Execute(c, t, "UPDATE challenges SET used=true WHERE token_hash=@h", ("h", stored));
        Execute(c, t, "UPDATE users SET verified=true WHERE id=@u", ("u", user));
        Execute(c, t, "DELETE FROM dev_mailbox WHERE user_id=@u AND purpose='verify'", ("u", user));
        Record(c, t, user, "account.verified", user, HttpContext.TraceIdentifier); t.Commit();
        return Data(new { verified = true });
    }
    [HttpPost("password/forgot")]
    public IActionResult Forgot(ForgotRequest request)
    {
        using var c = Database.Open(); using var t = c.BeginTransaction();
        var user = Scalar(c, t, "SELECT id FROM users WHERE lower(email)=lower(@e) AND active",
            ("e", request.Email.Trim())) as string;
        if (user is not null)
        {
            Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", "reset:" + user));
            var recent = Convert.ToInt64(Scalar(c, t, "SELECT count(*) FROM challenges WHERE user_id=@u AND purpose='reset' AND expires>@recent",
                ("u", user), ("recent", Now + 840))) > 0;
            if (!recent) Challenge(c, t, user, "reset");
        }
        t.Commit();
        return Accepted(new ApiResponse<object?>(StatusCodes.Status202Accepted, "Si corresponde, revise el mensaje de recuperación.", null));
    }
    [HttpPost("password/reset")]
    public IActionResult Reset(ResetRequest request)
    {
        using var c = Database.Open(); using var t = c.BeginTransaction();
        var user = Scalar(c, t, """
            UPDATE challenges SET used=true WHERE token_hash=@h AND purpose='reset'
            AND NOT used AND expires>@now RETURNING user_id
            """, ("h", Hash(request.Token)), ("now", Now)) as string;
        if (user is null) return Failure(400, "INVALID_CHALLENGE");
        Execute(c, t, "UPDATE users SET password_hash=@h,failed_logins=0,locked_until=0 WHERE id=@u",
            ("h", Password(request.Password)), ("u", user));
        Execute(c, t, "UPDATE sessions SET revoked=true WHERE user_id=@u", ("u", user));
        Execute(c, t, "DELETE FROM dev_mailbox WHERE user_id=@u AND purpose='reset'", ("u", user));
        Record(c, t, user, "account.password_reset", user, HttpContext.TraceIdentifier); t.Commit();
        return Data(new { reset = true });
    }
    [HttpGet("dev-mailbox")]
    public IActionResult Mailbox([FromQuery] string email, [FromQuery] string purpose)
    {
        if (mail.Mode != "development") return NotFound();
        var expected = configuration["Milestone:MailboxKey"];
        var supplied = Request.Headers["X-Dev-Mailbox-Key"].ToString();
        if (string.IsNullOrEmpty(expected) || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(Hash(expected)), System.Text.Encoding.UTF8.GetBytes(Hash(supplied))))
            return NotFound();
        using var c = Database.Open();
        var rows = Rows(c, null, """
            SELECT m.token,m.expires FROM dev_mailbox m JOIN users u ON u.id=m.user_id
            WHERE lower(u.email)=lower(@e) AND m.purpose=@p AND m.expires>@n
            """, ("e", email), ("p", purpose), ("n", Now));
        return rows.Count == 0 ? NotFound() : Data(rows[0]);
    }
    [HttpPost("login")]
    public IActionResult Login(LoginRequest request)
    {
        using var c = Database.Open(); using var t = c.BeginTransaction();
        var rows = Rows(c, t, "SELECT * FROM users WHERE lower(email)=lower(@e) FOR UPDATE",
            ("e", request.Email.Trim()));
        var row = rows.FirstOrDefault();
        var passwordValid = CheckPassword(row?["password_hash"] as string ?? DummyHash, request.Password);
        var valid = row is not null && passwordValid && (bool)row["active"]! && (bool)row["verified"]!
            && (long)row["locked_until"]! <= Now;
        if (!valid)
        {
            if (row is not null)
            {
                Execute(c, t, """
                    UPDATE users SET failed_logins=failed_logins+1,
                    locked_until=CASE WHEN failed_logins>=4 THEN @until ELSE locked_until END WHERE id=@u
                    """, ("until", Now + 300), ("u", row["id"]));
                Record(c, t, (string)row["id"]!, "account.login_denied", (string)row["id"]!, HttpContext.TraceIdentifier);
            }
            t.Commit(); return Failure(401, "INVALID_CREDENTIALS");
        }
        Execute(c, t, "UPDATE users SET failed_logins=0,locked_until=0 WHERE id=@u",
            ("u", row!["id"]));
        var result = CreateSession(c, t, (string)row["id"]!, (string)row["role"]!);
        Record(c, t, (string)row["id"]!, "session.created", (string)row["id"]!, HttpContext.TraceIdentifier);
        t.Commit(); return Data(result);
    }
    private object CreateSession(NpgsqlConnection c, NpgsqlTransaction t, string user, string role, long? absoluteExpiry = null, string? familyId = null)
    {
        var access = NewToken(); var refresh = NewToken(); var expires = absoluteExpiry ?? Now + 28800;
        var sessionId = Guid.NewGuid().ToString();
        Execute(c, t, """
            INSERT INTO sessions(id,user_id,access_hash,refresh_hash,access_expires,expires,family_id)
            VALUES(@id,@u,@a,@r,@ae,@e,@family)
            """, ("id", sessionId), ("family", familyId ?? sessionId), ("u", user), ("a", Hash(access)), ("r", Hash(refresh)),
            ("ae", Now + 1800), ("e", expires));
        SetCookies(access, refresh);
        // Browser client requests cookies; secrets never enter browser storage or response JSON.
        return Request.Headers["X-Session-Mode"] == "cookie"
            ? new { userId = user, role } : (object)new { userId = user, role, accessToken = access, refreshToken = refresh, expiresIn = 1800 };
    }
    private void SetCookies(string access, string refresh)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromHours(8)
        };
        Response.Cookies.Append("tm_access", access, options);
        Response.Cookies.Append("tm_refresh", refresh, options);
    }
    [HttpPost("refresh")]
    public IActionResult Refresh(RefreshRequest request)
    {
        var token = request.RefreshToken ?? Request.Cookies["tm_refresh"];
        if (string.IsNullOrEmpty(token)) return Failure(401, "SESSION_REQUIRED");
        using var c = Database.Open(); using var t = c.BeginTransaction();
        var rows = Rows(c, t, """
            SELECT s.id,s.user_id,s.expires,s.family_id,u.role FROM sessions s JOIN users u ON u.id=s.user_id
            WHERE s.refresh_hash=@h AND NOT s.revoked AND s.expires>@now AND u.active AND u.verified
            FOR UPDATE OF s
            """, ("h", Hash(token)), ("now", Now));
        if (rows.Count == 0) return Failure(401, "SESSION_REQUIRED");
        var row = rows[0];
        Execute(c, t, "UPDATE sessions SET revoked=true WHERE id=@id", ("id", row["id"]));
        var result = CreateSession(c, t, (string)row["user_id"]!, (string)row["role"]!, (long)row["expires"]!, (string)row["family_id"]!);
        t.Commit(); return Data(result);
    }
    [Authorize, HttpGet("me")]
    public IActionResult Me() => Data(new
    {
        userId = UserId(User),
        email = User.Identity!.Name,
        role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)!.Value
    });
    [Authorize, HttpPost("logout")]
    public IActionResult Logout()
    {
        using var c = Database.Open(); using var t = c.BeginTransaction();
        Execute(c, t, "UPDATE sessions SET revoked=true WHERE user_id=@u AND family_id=(SELECT family_id FROM sessions WHERE id=@id)",
            ("u", UserId(User)), ("id", User.FindFirst("session_id")!.Value));
        Record(c, t, UserId(User), "session.revoked", UserId(User), HttpContext.TraceIdentifier); t.Commit();
        Response.Cookies.Delete("tm_access"); Response.Cookies.Delete("tm_refresh");
        return Data(new { loggedOut = true });
    }
}
