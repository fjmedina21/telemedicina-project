using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Telemedicina.Api.Features.Scheduling;

public readonly record struct ScheduleRange(DateTime FromUtc, DateTime ToUtc);
public static class ScheduleInputs
{
    public static bool TryId(string? value, bool required, out string id)
    {
        id = "";
        if (value is null) return !required;
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty) return false;
        id = parsed.ToString(); return true;
    }
    public static bool TryTimestamp(string? value, out DateTimeOffset result)
    {
        result = default;
        return value is not null && Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d{1,7})?)?(?:Z|[+-]\d{2}:\d{2})$", RegexOptions.CultureInvariant)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
    public static string? Range(string? from, string? to, DateTimeOffset now, out ScheduleRange range)
    {
        range = default;
        var begin = now;
        if (from is not null && !TryTimestamp(from, out begin)) return "VALIDATION_FAILED";
        DateTimeOffset end;
        if (to is not null)
        {
            if (!TryTimestamp(to, out end)) return "VALIDATION_FAILED";
        }
        else
        {
            if (begin > DateTimeOffset.MaxValue.AddDays(-14)) return "INVALID_RANGE";
            end = begin.AddDays(14);
        }
        if (end <= begin || end - begin > TimeSpan.FromDays(31)) return "INVALID_RANGE";
        range = new(begin.UtcDateTime, end.UtcDateTime); return null;
    }
    public static bool TryBoolean(string? value, out bool? result)
    {
        result = null;
        if (value is null) return true;
        if (!bool.TryParse(value, out var parsed)) return false;
        result = parsed; return true;
    }
}
public sealed class ExplicitOffsetDateTimeConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !ScheduleInputs.TryTimestamp(reader.GetString(), out var value))
            throw new JsonException("A date with an explicit UTC offset is required.");
        return value;
    }
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToUniversalTime());
}
