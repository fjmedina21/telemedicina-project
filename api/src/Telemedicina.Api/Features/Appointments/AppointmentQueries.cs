namespace Telemedicina.Api.Features.Appointments;

internal static class AppointmentQueries
{
    // One supplied UTC instant governs the projection and filtering for each query.
    public const string Select = """
        SELECT a.id,a.user_id AS "patientId",u.email AS "patientEmail",a.slot_id AS "slotId",s.service_id AS "serviceId",
            a.practitioner_id AS "practitionerId",p.name AS "practitionerName",v.name AS "serviceName",v.specialty,
            a.start_utc AS "startAt",a.end_utc AS "endAt",a.status,
            effective.status AS "coverageStatus",'Manual' AS "coverageMethod",
            (effective.status IN ('Pending','ManualReview','Expired')) AS "coverageRequiresReview",
            validity.reason AS "coverageValidityReason",
            a.insurer,a.member_last4 AS "memberLast4",a.version,a.evidence,
            a.reviewed_by AS "reviewedBy",a.reviewed_at AS "reviewedAt",a.coverage_valid_until AS "validUntil"
        FROM appointments a JOIN slots s ON s.id=a.slot_id JOIN services v ON v.id=s.service_id
        JOIN practitioners p ON p.id=a.practitioner_id JOIN users u ON u.id=a.user_id
        CROSS JOIN LATERAL (
            SELECT CASE WHEN a.coverage<>'Approved' THEN NULL
                WHEN a.coverage_valid_until IS NULL THEN 'MissingValidity'
                WHEN a.coverage_valid_until<=@nowUtc THEN 'Expired'
                WHEN a.coverage_valid_until<a.end_utc THEN 'OutsideAppointment'
                ELSE NULL END AS reason
        ) validity
        CROSS JOIN LATERAL (
            SELECT CASE WHEN validity.reason='Expired' THEN 'Expired'
                WHEN validity.reason IN ('MissingValidity','OutsideAppointment') THEN 'ManualReview'
                ELSE a.coverage END AS status
        ) effective
        """;
}
