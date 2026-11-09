using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Appointments;

public sealed record CheckInRequest(
    [Range(0, int.MaxValue)] int Version,
    bool Prepared,
    bool PrivacyConfirmed,
    bool ConnectivityConfirmed,
    bool ConsentAccepted,
    [Range(1, 1)] int ConsentVersion,
    bool TechnicalPreflightCompleted);

public sealed record CheckInConsentSnapshot(
    int Version, string Hash, string Text, bool Accepted);

public sealed record PatientCheckInSnapshot(
    string Status, bool CanProceed, int ConfigurationVersion, int Version,
    DateTime WindowOpenAt, DateTime WindowCloseAt,
    bool Prepared, bool PrivacyConfirmed, bool ConnectivityConfirmed,
    bool TechnicalPreflightCompleted, CheckInConsentSnapshot Consent,
    string[] Blockers);

// Admission receives status and operational timestamps only. Declarations,
// preflight and consent material are intentionally absent from this contract.
public sealed record AdmissionCheckInStatus(
    string AppointmentId, string Status, int ConfigurationVersion,
    int AttemptVersion, DateTime WindowOpenAt, DateTime WindowCloseAt,
    DateTime? UpdatedAt, DateTime? ReadyAt);
