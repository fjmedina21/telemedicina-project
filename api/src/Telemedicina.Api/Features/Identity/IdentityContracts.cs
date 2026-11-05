using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Identity;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password);
public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);
public sealed record VerificationRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, RegularExpression("^[A-Za-z0-9]{6}$")] string Token);
public sealed record RefreshRequest([MaxLength(128)] string? RefreshToken = null);
public sealed record ForgotRequest([Required, EmailAddress, MaxLength(254)] string Email);
public sealed record ResetRequest(
    [Required, MaxLength(128)] string Token,
    [Required, MinLength(12), MaxLength(128)] string Password);
public sealed record ActiveRequest(bool Active);
