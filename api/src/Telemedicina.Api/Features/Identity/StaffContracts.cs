using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Identity;

public sealed record InviteStaffRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, RegularExpression("^(Admission|Practitioner|Support)$")] string Role);
public sealed record StaffVersionRequest([Range(1, int.MaxValue)] int ExpectedVersion);
public sealed record StaffRoleRequest(
    [Required, RegularExpression("^(Admission|Practitioner|Support)$")] string Role,
    [Range(1, int.MaxValue)] int ExpectedVersion);
public sealed record StaffActiveRequest([property: System.Text.Json.Serialization.JsonRequired] bool Active, [Range(1, int.MaxValue)] int ExpectedVersion);
public sealed record ActivateStaffRequest(
    [Required, RegularExpression("^[A-Fa-f0-9]{64}$")] string Token,
    [Required, MinLength(12), MaxLength(128)] string Password);
public sealed record StaffAccount(string Id, string Email, string Role, string Status, int Version);
public sealed record StaffPage(IReadOnlyList<StaffAccount> Items, string? Next);
public sealed record StaffAuditPage(IReadOnlyList<Dictionary<string, object?>> Items, long? Next);

public sealed record StaffActivationResult(bool Activated);
public sealed record IdentityResponse(string UserId, string Email, string Role, string[] Permissions, int AccountVersion);
