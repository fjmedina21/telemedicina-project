using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Telemedicina.Api.Features.Scheduling;

public sealed record SlotRequest(Guid ServiceId, Guid PractitionerId,
    [property: JsonConverter(typeof(ExplicitOffsetDateTimeConverter))] DateTimeOffset StartAt);
public sealed record BlockRequest([property: JsonRequired] bool Blocked, [Range(1, int.MaxValue)] int? Version = null);
public sealed record SlotCreated(string Id, int Version);
public sealed record SlotBlocked(bool Blocked, int Version);
public sealed record AvailabilityItem(string Id, string ServiceId, string PractitionerId, string PractitionerName,
    DateTimeOffset StartAt, DateTimeOffset EndAt, string ServiceName, string Specialty);
public sealed record AdminSlotItem(string Id, string ServiceId, string ServiceName, string PractitionerId,
    string PractitionerName, DateTimeOffset StartAt, DateTimeOffset EndAt, bool Blocked, bool Reserved,
    int Version, bool ServiceActive, bool PractitionerActive, bool AssignmentActive, bool ServicePublishable);
public sealed record AdminSlotPage(List<AdminSlotItem> Items, long TotalCount, int Offset, int Limit, bool HasMore);
