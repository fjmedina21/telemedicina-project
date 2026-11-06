using System.Text.Json.Serialization;
using Telemedicina.Api.Features.Scheduling;
using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Admission;

public sealed record CoverageRequest(
    [Required, RegularExpression("^(Approved|Denied|ManualReview)$")] string Status,
    [Required, MaxLength(250)] string Evidence,
    [property: JsonConverter(typeof(ExplicitOffsetDateTimeConverter))] DateTimeOffset? ValidUntil,
    [Range(1, int.MaxValue)] int Version);
