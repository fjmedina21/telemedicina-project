using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Scheduling;

public sealed record SlotRequest(Guid ServiceId, Guid PractitionerId, DateTimeOffset StartAt);
public sealed record BlockRequest(bool Blocked);
