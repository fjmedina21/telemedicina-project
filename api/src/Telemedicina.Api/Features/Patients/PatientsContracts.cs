using System.Text.Json;
using System.Text.Json.Serialization;

namespace Telemedicina.Api.Features.Patients;

// Full replacement; normalize and validate in ProfileRules, before persistence.
public sealed record ProfileRequest(
    string FirstName, string LastName, DateOnly BirthDate, string Phone,
    string Address, string EmergencyName, string EmergencyPhone,
    string[] Allergies, string[] Medications, string[] Conditions,
    [property: JsonRequired] int Version)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }
}

public sealed record ProfileCompleteness(string Status, string[] MissingFields);
public sealed record DeclarationMetadata(string Source, DateTime? UpdatedAt);
public sealed record ProfileSnapshot(JsonElement Profile, int Version, DateTime UpdatedAt,
    string Source, Dictionary<string, DeclarationMetadata> DeclarationMetadata, ProfileCompleteness Completeness);
public sealed record ProfileSaved(int Version);
