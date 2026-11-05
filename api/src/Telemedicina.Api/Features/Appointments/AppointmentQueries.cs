namespace Telemedicina.Api.Features.Appointments;

internal static class AppointmentQueries
{
    public const string Select = """
        SELECT a.id,a.user_id AS "patientId",u.email AS "patientEmail",a.slot_id AS "slotId",s.service_id AS "serviceId",
            p.name AS "practitionerName",v.name AS "serviceName",v.specialty,
            a.start_utc AS "startAt",a.end_utc AS "endAt",a.status,
            CASE WHEN a.coverage='Approved' AND a.coverage_valid_until<now() THEN 'Expired' ELSE a.coverage END AS "coverageStatus",
            a.insurer,a.member_last4 AS "memberLast4",a.version,a.evidence,
            a.reviewed_by AS "reviewedBy",a.reviewed_at AS "reviewedAt",a.coverage_valid_until AS "validUntil"
        FROM appointments a JOIN slots s ON s.id=a.slot_id JOIN services v ON v.id=s.service_id
        JOIN practitioners p ON p.id=a.practitioner_id JOIN users u ON u.id=a.user_id
        """;
}
