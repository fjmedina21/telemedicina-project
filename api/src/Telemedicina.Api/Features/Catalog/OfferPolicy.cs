using Npgsql;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Features.Catalog;

public sealed record SlotIdentity(string Id, string ServiceId, string PractitionerId);
public sealed record OfferState(bool ServiceActive, bool PractitionerActive, bool Published, bool Assigned, int Duration);

/// <summary>Catalog rows are locked before slots; associations are read after those locks.</summary>
public static class OfferPolicy
{
    public const string PublicServiceSql = "v.active AND (v.is_synthetic OR length(btrim(coalesce(v.approval_reference,'')))>0)";
    public static bool IsPublished(bool active, bool synthetic, string? reference) =>
        active && (synthetic || !string.IsNullOrWhiteSpace(reference));

    public static void LockServices(NpgsqlConnection c, NpgsqlTransaction t, IEnumerable<string> ids)
    {
        foreach (var id in ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            Scalar(c, t, "SELECT id FROM services WHERE id=@id FOR SHARE", ("id", id));
    }
    public static void LockPractitioners(NpgsqlConnection c, NpgsqlTransaction t, IEnumerable<string> ids)
    {
        foreach (var id in ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            Scalar(c, t, "SELECT id FROM practitioners WHERE id=@id FOR SHARE", ("id", id));
    }
    public static void LockOffers(NpgsqlConnection c, NpgsqlTransaction t, IEnumerable<SlotIdentity> identities)
    {
        var rows = identities.ToArray();
        LockServices(c, t, rows.Select(r => r.ServiceId));
        LockPractitioners(c, t, rows.Select(r => r.PractitionerId));
    }
    public static List<SlotIdentity> Identities(NpgsqlConnection c, NpgsqlTransaction t, IEnumerable<string> ids) =>
        Rows(c, t, "SELECT id,service_id,practitioner_id FROM slots WHERE id=ANY(@ids)",
            ("ids", ids.Distinct(StringComparer.Ordinal).ToArray()))
        .Select(r => new SlotIdentity((string)r["id"]!, (string)r["service_id"]!, (string)r["practitioner_id"]!)).ToList();
    public static void LockSlots(NpgsqlConnection c, NpgsqlTransaction t, IEnumerable<string> ids)
    {
        foreach (var id in ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            Scalar(c, t, "SELECT id FROM slots WHERE id=@id FOR UPDATE", ("id", id));
    }
    public static OfferState? Read(NpgsqlConnection c, NpgsqlTransaction t, string service, string practitioner)
    {
        var row = Rows(c, t, """
            SELECT v.active AS service_active,p.active AS practitioner_active,v.duration,
                (v.active AND (v.is_synthetic OR length(btrim(coalesce(v.approval_reference,'')))>0)) AS published,
                EXISTS(SELECT 1 FROM practitioner_services ps WHERE ps.service_id=v.id AND ps.practitioner_id=p.id AND ps.active) AS assigned
            FROM services v CROSS JOIN practitioners p WHERE v.id=@s AND p.id=@p
            """, ("s", service), ("p", practitioner)).SingleOrDefault();
        return row is null ? null : new((bool)row["service_active"]!, (bool)row["practitioner_active"]!,
            (bool)row["published"]!, (bool)row["assigned"]!, (int)row["duration"]!);
    }
}
