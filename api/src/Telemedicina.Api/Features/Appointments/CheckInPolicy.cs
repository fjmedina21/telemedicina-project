using System.Globalization;
using System.Text.Json;

namespace Telemedicina.Api.Features.Appointments;

public static class CheckInPolicy
{
    public const int ConfigurationVersion = 1;
    public const int ConsentVersion = 1;
    public const string ConsentText =
        "Demostración académica: no constituye atención médica real y no debe utilizarse para emergencias.";
    public const string ConsentHash =
        "a7ae0d686693ad80daaef8b775d47b062484ad354ecb17a90df10ac865626631";

    public static DateTime OpensAt(DateTime start) => start.AddMinutes(-30);
    public static DateTime ClosesAt(DateTime start) => start.AddMinutes(15);
    public static bool InWindow(DateTime start, DateTime now) =>
        now >= OpensAt(start) && now <= ClosesAt(start);

    public static bool IsAdultSelf(JsonElement profile, DateOnly today)
    {
        if (profile.ValueKind != JsonValueKind.Object ||
            !profile.TryGetProperty("birthDate", out var value) ||
            value.ValueKind != JsonValueKind.String ||
            !DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var birthDate))
            return false;
        return birthDate.AddYears(18) <= today;
    }

    public static string State(string appointmentStatus, DateTime start, DateTime now,
        bool eligible, bool prepared, bool privacy, bool connectivity,
        bool consent, bool preflight)
    {
        if (appointmentStatus != "Confirmed") return "Rejected";
        if (now > ClosesAt(start)) return "Expired";
        if (!(prepared || privacy || connectivity || consent || preflight)) return "NotStarted";
        if (!(eligible && prepared && privacy && connectivity && consent)) return "InProgress";
        if (!preflight || now < OpensAt(start)) return "Verified";
        return "Ready";
    }
}
