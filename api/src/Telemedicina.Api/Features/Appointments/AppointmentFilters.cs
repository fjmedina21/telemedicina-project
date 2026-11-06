using Telemedicina.Api.Features.Scheduling;
namespace Telemedicina.Api.Features.Appointments;

/// <summary>Raw query values distinguish omitted filters from invalid empty values.</summary>
public sealed record AppointmentFilters(DateTime? FromUtc, DateTime? ToUtc, string Status, string CoverageStatus)
{
    public static string? Parse(string? from, string? to, string? status, string? coverageStatus, out AppointmentFilters filters)
    {
        filters = new(null, null, status ?? "", coverageStatus ?? "");
        if (status is not null && status is not ("PendingValidation" or "Confirmed" or "Cancelled" or "Rejected")) return "VALIDATION_FAILED";
        if (coverageStatus is not null && coverageStatus is not ("Pending" or "ManualReview" or "Approved" or "Denied" or "Expired")) return "VALIDATION_FAILED";
        if (from is null && to is null) return null;
        if (from is null || to is null) return "INVALID_RANGE";
        if (!ScheduleInputs.TryTimestamp(from, out var begin) || !ScheduleInputs.TryTimestamp(to, out var end)) return "VALIDATION_FAILED";
        if (end <= begin || end - begin > TimeSpan.FromDays(366)) return "INVALID_RANGE";
        filters = filters with { FromUtc = begin.UtcDateTime, ToUtc = end.UtcDateTime };
        return null;
    }
    public const string Sql = """
        AND (@from::timestamptz IS NULL OR a.start_utc>=@from::timestamptz)
        AND (@to::timestamptz IS NULL OR a.start_utc<@to::timestamptz)
        AND (@status='' OR a.status=@status)
        AND (@coverage='' OR effective.status=@coverage)
        """;
    public (string, object?)[] Parameters(DateTime nowUtc) =>
        [("from", FromUtc), ("to", ToUtc), ("status", Status), ("coverage", CoverageStatus), ("nowUtc", nowUtc)];
}
