using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Admission;

public sealed record CoverageRequest(
    [Required, RegularExpression("^(Approved|Denied|ManualReview)$")] string Status,
    [Required, MaxLength(250)] string Evidence,
    DateTimeOffset? ValidUntil,
    [Range(1, int.MaxValue)] int Version);
