using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Appointments;

public sealed record BookRequest(
    [Required] Guid SlotId,
    [Required, MaxLength(100)] string Insurer,
    [Required, RegularExpression(@"^[0-9]{4}$")] string MemberLast4);
public sealed record RescheduleRequest(Guid SlotId, [Range(1, int.MaxValue)] int Version);
