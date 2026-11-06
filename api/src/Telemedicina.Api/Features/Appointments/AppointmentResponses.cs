namespace Telemedicina.Api.Features.Appointments;

// OpenAPI describes the existing envelope payloads, including additive H05 fields.
public sealed record AppointmentSnapshot(
    string Id, string PatientId, string PatientEmail, string SlotId, string ServiceId,
    string PractitionerId, string PractitionerName, string ServiceName, string Specialty,
    DateTime StartAt, DateTime EndAt, string Status, string CoverageStatus,
    string CoverageMethod, bool CoverageRequiresReview, string? CoverageValidityReason,
    string Insurer, string MemberLast4, int Version, string? Evidence, string? ReviewedBy,
    DateTime? ReviewedAt, DateTime? ValidUntil);
public sealed record AppointmentBooked(string Id, string Status, string CoverageStatus, int Version);
public sealed record AppointmentCancelled(bool Cancelled);
public sealed record AppointmentRescheduled(bool Rescheduled);
public sealed record CoverageReviewed(string Status, string CoverageStatus, int Version);
public sealed record AppointmentHistoryItem(long Sequence, string? OldSlotId, string NewSlotId,
    string? OldStatus, string NewStatus, string? OldCoverage, string NewCoverage,
    int Version, string ActorId, DateTime OccurredAt);
public sealed record CheckInRequirementsSnapshot(string Status, bool CanProceed, int? ConfigurationVersion);
