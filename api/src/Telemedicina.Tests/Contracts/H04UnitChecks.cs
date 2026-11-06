using System.Text.Json;
using Telemedicina.Api.Features.Catalog;
using Telemedicina.Api.Features.Scheduling;
using Telemedicina.Api.Features.Appointments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Telemedicina.Common.Models.ApiResponses;

namespace Telemedicina.Tests;

public static class H04UnitChecks
{
    public static void Register(Action<string, Action> test, Action<bool, string> assert)
    {
        test("H04_BookingBindingErrorsExposeSafeFieldCodesOnly", () =>
        {
            var http = new DefaultHttpContext();
            http.Request.Path = "/api/v1/appointments";
            var state = new ModelStateDictionary();
            state.AddModelError("request.MemberLast4", "synthetic-secret-value");
            var context = new ActionContext(http, new RouteData(), new ActionDescriptor(), state);
            var result = (BadRequestObjectResult)AppointmentBindingErrors.Create(context);
            var body = (ErrorApiResponse)result.Value!;
            assert(body.Code == "VALIDATION_FAILED" && body.Errors.Contains("memberLast4:INVALID"),
                "Expose stable field code.");
            assert(!body.Errors.Any(value => value.Contains("synthetic-secret-value", StringComparison.Ordinal)) &&
                !body.Detail.Contains("synthetic-secret-value", StringComparison.Ordinal),
                "Never echo submitted values or framework messages.");
        });
        test("H04_PublicationRejectsUnknownHistoricalApproval", () =>
        {
            assert(!OfferPolicy.IsPublished(true, false, null), "Historical absence is not an approval.");
            assert(!OfferPolicy.IsPublished(true, false, "  "), "Whitespace is not an approval reference.");
            assert(OfferPolicy.IsPublished(true, true, null), "Synthetic development offering remains visible.");
            assert(OfferPolicy.IsPublished(true, false, "synthetic-reference"), "Explicit reference permits publication.");
            assert(!OfferPolicy.IsPublished(false, true, "reference"), "Inactive services never publish.");
        });
        test("H04_RangeUsesEffectiveFromAndRejectsUnzonedValues", () =>
        {
            var now = DateTimeOffset.Parse("2026-11-05T10:00:00Z");
            assert(ScheduleInputs.Range("2026-12-01T10:00:00-04:00", null, now, out var range) is null &&
                range.ToUtc-range.FromUtc == TimeSpan.FromDays(14) && range.FromUtc.Hour==14, "Default end follows effective start.");
            assert(ScheduleInputs.Range("2026-12-01T10:00:00", null, now, out _) == "VALIDATION_FAILED", "Require explicit offset.");
            assert(ScheduleInputs.Range("2026-12-01T10:00:00Z", "2027-01-02T10:00:00Z", now, out _) == "INVALID_RANGE", "Maximum 31 days.");
            assert(ScheduleInputs.Range("2026-12-01T10:00:00Z", "2026-12-01T10:00:00Z", now, out _) == "INVALID_RANGE", "Positive interval required.");
        });
        test("H04_InvalidOptionalFiltersAreNotSilentlyOmitted", () =>
        {
            assert(ScheduleInputs.TryId(null, false, out _) && !ScheduleInputs.TryId("", false, out _), "Omitted differs from empty.");
            assert(!ScheduleInputs.TryId(Guid.Empty.ToString(), true, out _) && !ScheduleInputs.TryId(null, true, out _), "Require nonempty UUID.");
            assert(!ScheduleInputs.TryBoolean("", out _) && ScheduleInputs.TryBoolean("false", out var value) && value==false, "Boolean filter must be valid.");
        });
        test("H04_BindingPreservesLegacyButRequiresBlocked", () =>
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var legacy = JsonSerializer.Deserialize<BlockRequest>("""{"blocked":false}""", options)!;
            assert(!legacy.Blocked && legacy.Version is null, "Legacy assignment remains available.");
            var rejected = false;
            try { JsonSerializer.Deserialize<BlockRequest>("{}", options); } catch(JsonException) { rejected=true; }
            assert(rejected, "Missing boolean is not a deliberate unblock.");
            rejected=false;
            try { JsonSerializer.Deserialize<SlotRequest>("""{"serviceId":"10000000-0000-4000-8000-000000000001","practitionerId":"20000000-0000-4000-8000-000000000001","startAt":"2026-12-01T10:00:00"}""", options); }
            catch(JsonException) { rejected=true; }
            assert(rejected, "Slot body must carry an explicit offset.");
        });
    }
}
