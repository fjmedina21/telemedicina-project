using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Common.Models.ApiResponses;

namespace Telemedicina.Api.Features.Patients;

public static class ProfileRules
{
    public static readonly string[] Fields = ["firstName", "lastName", "birthDate", "phone", "address",
        "emergencyName", "emergencyPhone", "allergies", "medications", "conditions"];
    public static readonly string[] Categories = ["allergies", "medications", "conditions"];
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "userId", "patientId", "documentType", "documentNumber", "identificationCard", "verifiedAt",
        "email", "role", "roles", "status", "active", "verified", "accountStatus", "accountState",
        "declarationMetadata", "completeness", "source", "updatedAt"
    };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ProfileRequest Normalize(ProfileRequest r) => r with
    {
        FirstName = r.FirstName?.Trim()!, LastName = r.LastName?.Trim()!, Phone = r.Phone?.Trim()!,
        Address = r.Address?.Trim()!, EmergencyName = r.EmergencyName?.Trim()!, EmergencyPhone = r.EmergencyPhone?.Trim()!,
        Allergies = NormalizeList(r.Allergies)!, Medications = NormalizeList(r.Medications)!, Conditions = NormalizeList(r.Conditions)!
    };
    private static string[]? NormalizeList(string[]? values) => values?.Select(v => v?.Trim()!).ToArray();

    public static string[] Validate(ProfileRequest r)
    {
        var errors = ValidateBody(JsonSerializer.SerializeToElement(r, Json)).ToList();
        if (r.Version < 0) errors.Add("version:INVALID");
        if (r.AdditionalProperties?.Keys.Any(Protected.Contains) == true) errors.Add("profile:INVALID");
        return errors.ToArray();
    }

    public static ProfileCompleteness Completeness(JsonElement body)
    {
        var missing = ValidateBody(body).Select(e => e.Split(':')[0]).Distinct().ToArray();
        return new(missing.Length == 0 ? "Complete" : "Incomplete", missing);
    }

    public static string[] ValidateBody(JsonElement body)
    {
        var errors = new List<string>();
        foreach (var field in Fields)
        {
            if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null)
            { errors.Add(field + ":REQUIRED"); continue; }
            if (Categories.Contains(field))
            {
                if (value.ValueKind != JsonValueKind.Array) errors.Add(field + ":INVALID");
                else if (value.GetArrayLength() > 20) errors.Add(field + ":MAX_ITEMS");
                else if (value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(v.GetString())))
                    errors.Add(field + ":INVALID");
                else if (value.EnumerateArray().Any(v => v.GetString()!.Trim().Length > 200)) errors.Add(field + ":MAX_LENGTH");
                continue;
            }
            if (value.ValueKind != JsonValueKind.String) { errors.Add(field + ":INVALID"); continue; }
            var text = value.GetString()!.Trim();
            if (text.Length == 0) { errors.Add(field + ":REQUIRED"); continue; }
            if (field == "birthDate")
            {
                if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                    date.Year < 1900 || date >= DateOnly.FromDateTime(DateTime.UtcNow)) errors.Add(field + ":INVALID");
                continue;
            }
            var max = field switch { "firstName" or "lastName" => 80, "phone" or "emergencyPhone" => 30,
                "address" => 250, _ => 100 };
            if (text.Length > max) errors.Add(field + ":MAX_LENGTH");
            else if ((field is "phone" or "emergencyPhone") && !new PhoneAttribute().IsValid(text)) errors.Add(field + ":INVALID");
        }
        return errors.ToArray();
    }

    public static bool SameCategory(JsonElement previous, string field, string[] current)
    {
        if (previous.ValueKind != JsonValueKind.Object || !previous.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String)) return false;
        return value.EnumerateArray().Select(v => v.GetString()!.Trim()).SequenceEqual(current, StringComparer.Ordinal);
    }

    // Never expose model-binding messages: JsonException may contain submitted values.
    public static IActionResult BindingFailure(ActionContext context)
    {
        var errors = context.ModelState.Where(p => p.Value?.Errors.Count > 0).Select(p =>
        {
            var key = p.Key.TrimStart('$', '.').Split('.', '[')[0];
            var field = Fields.Append("version").FirstOrDefault(f => f.Equals(key, StringComparison.OrdinalIgnoreCase));
            return (field ?? "profile") + ":INVALID";
        }).Distinct().ToArray();
        return new BadRequestObjectResult(new ErrorApiResponse(400, "Los datos enviados no son válidos.",
            context.HttpContext.Request.Path.Value ?? "/", errors)
        { Code = "VALIDATION_FAILED", TraceId = context.HttpContext.TraceIdentifier });
    }
}
