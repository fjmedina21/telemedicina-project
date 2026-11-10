using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Catalog;

public sealed record CatalogServiceRequest(
    [Required, MaxLength(120)] string Name, [Required, MaxLength(100)] string Specialty,
    [Range(5, 240)] int DurationMinutes, bool Active, bool IsSynthetic,
    [MaxLength(250)] string? ApprovalReference, [Range(0, int.MaxValue)] int Version = 0);
public sealed record CatalogPractitionerRequest(
    [Required, MaxLength(120)] string Name, [MaxLength(100)] string? LicenseRef,
    bool Active, [Required, MaxLength(100)] string[] ServiceIds, string? AccountId = null,
    [Range(0, int.MaxValue)] int Version = 0);

public sealed record SpecialtySummary(string Name);
public sealed record ServiceSummary(string Id, string Name, string Specialty, int DurationMinutes);
public sealed record PractitionerSummary(string Id, string Name);
