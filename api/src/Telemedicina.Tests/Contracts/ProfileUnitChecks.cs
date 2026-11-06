using System.Text.Json;
using Telemedicina.Api.Features.Patients;

namespace Telemedicina.Tests;

public static class ProfileUnitChecks
{
    public static ProfileRequest Valid() => new(" Paciente ", " Sintético ", new DateOnly(1990,1,1), "8095550101",
        " Dirección sintética ", " Contacto ", "8095550102", [" Dato, sintético "], [], [], 0);
    public static void Register(Action<string, Action> test, Action<bool, string> assert)
    {
        test("H03_TrimAndCommaArePreserved", () =>
        {
            var r = ProfileRules.Normalize(Valid() with { FirstName = " " + new string('A',80) + " " });
            assert(ProfileRules.Validate(r).Length == 0 && r.FirstName.Length == 80 && r.Allergies[0] == "Dato, sintético",
                "Trim before validation; commas remain within a declaration.");
        });
        test("H03_FieldErrorsAreSafeAndDeterministic", () =>
        {
            var errors = ProfileRules.Validate(ProfileRules.Normalize(Valid() with
                { FirstName = " ", Phone = "invalid", Allergies = [" "], Medications = Enumerable.Repeat("x",21).ToArray(), Version = -1 }));
            assert(errors.SequenceEqual(new[] {"firstName:REQUIRED","phone:INVALID","allergies:INVALID","medications:MAX_ITEMS","version:INVALID"}),
                "Only stable field names and codes.");
        });
        test("H03_CompletenessAbsentAndHistorical", () =>
        {
            assert(ProfileRules.Completeness(default).MissingFields.SequenceEqual(ProfileRules.Fields), "All missing fields in approved order.");
            var body = JsonSerializer.SerializeToElement(ProfileRules.Normalize(Valid()), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            assert(ProfileRules.Completeness(body).Status == "Complete", "Empty arrays satisfy presence.");
            using var historic = JsonDocument.Parse("{\"allergies\":null,\"medications\":[],\"conditions\":[42]}");
            var missing = ProfileRules.Completeness(historic.RootElement).MissingFields;
            assert(missing.Contains("allergies") && !missing.Contains("medications") && missing.Contains("conditions"), "Invalid historical values are missing.");
        });
        test("H03_IdentityAndMetadataAreNotWritable", () =>
        {
            foreach (var field in new[] {"patientId","USERID","documentType","documentNumber","identificationCard","verifiedAt","email","roles","status","accountStatus","declarationMetadata","completeness"})
            {
                var r = Valid() with { AdditionalProperties = new() { [field] = JsonSerializer.SerializeToElement("synthetic") } };
                assert(ProfileRules.Validate(r).Contains("profile:INVALID"), "Protected fields must fail without echo.");
            }
        });
        test("H03_CategoryComparisonHasNoClinicalNormalization", () =>
        {
            using var doc = JsonDocument.Parse("""{"allergies":[" A ","a","A"]}""");
            assert(ProfileRules.SameCategory(doc.RootElement,"allergies",["A","a","A"]), "Only trim strings.");
            assert(!ProfileRules.SameCategory(doc.RootElement,"allergies",["A","A","a"]), "Order, case and duplicates remain significant.");
            assert(!ProfileRules.SameCategory(doc.RootElement,"allergies",[]), "Clearing a category changes it.");
        });
        test("H03_RequestRequiresVersion", () =>
        {
            var rejected=false;
            try { JsonSerializer.Deserialize<ProfileRequest>("{}",new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch(JsonException) { rejected=true; }
            assert(rejected,"Full replacement requires an explicit version.");
        });
    }
}
