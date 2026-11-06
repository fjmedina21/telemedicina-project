using System.Text.Json;
using Telemedicina.Api.Features.Appointments;
using Telemedicina.Api.Features.Admission;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
namespace Telemedicina.Tests;

public static class H05UnitChecks
{
    public static void Register(Action<string,Action> test,Action<bool,string> assert)
    {
        test("H05_BookContractPreservesHistoricalFingerprint",()=>{
            var request=new BookRequest(Guid.Parse("10000000-0000-4000-8000-000000000001"),"Synthetic","1234");
            var json=JsonSerializer.Serialize(request,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            const string historical="""{"slotId":"10000000-0000-4000-8000-000000000001","insurer":"Synthetic","memberLast4":"1234"}""";
            assert(json==historical,"Preserve exact serialized fields and order.");
            assert(Hash(json)==Hash(historical),"Historical request fingerprint remains unchanged.");
        });
        test("H05_FiltersRequirePairedOffsetDatesAndKnownStates",()=>{
            assert(AppointmentFilters.Parse(null,null,null,null,out _) is null,"Legacy unfiltered lists remain supported.");
            assert(AppointmentFilters.Parse("2040-01-01T00:00:00Z",null,null,null,out _)=="INVALID_RANGE","Paired endpoints required.");
            assert(AppointmentFilters.Parse("2040-01-01T00:00:00","2040-01-02T00:00:00Z",null,null,out _)=="VALIDATION_FAILED","Explicit offset required.");
            assert(AppointmentFilters.Parse("2040-01-01T00:00:00-04:00","2040-01-02T00:00:00Z","Confirmed","Expired",out var f) is null &&
                f.FromUtc!.Value.Hour==4,"Offset dates normalize to UTC.");
            assert(AppointmentFilters.Parse("2040-01-01T00:00:00Z","2042-01-01T00:00:00Z",null,null,out _)=="INVALID_RANGE","At most 366 days.");
            foreach(var status in new[]{"","confirmed","Completed"})
                assert(AppointmentFilters.Parse(null,null,status,null,out _)=="VALIDATION_FAILED","Reject invalid appointment state.");
            foreach(var coverage in new[]{"","approved","NotRequired"})
                assert(AppointmentFilters.Parse(null,null,null,coverage,out _)=="VALIDATION_FAILED","Reject invalid coverage state.");
        });
        test("H05_CoverageDatesRequireOffsetAndAllowNull",()=>{
            var options=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var valid=JsonSerializer.Deserialize<CoverageRequest>("""{"status":"Approved","evidence":"synthetic","validUntil":"2040-01-01T00:00:00-04:00","version":1}""",options)!;
            assert(valid.ValidUntil!.Value.UtcDateTime.Hour==4,"Offset is preserved semantically.");
            var denied=JsonSerializer.Deserialize<CoverageRequest>("""{"status":"Denied","evidence":"synthetic","validUntil":null,"version":1}""",options)!;
            assert(denied.ValidUntil is null,"Null validity remains supported.");
            foreach(var value in new[]{JsonSerializer.Serialize("2040-01-01T00:00:00"),"123",JsonSerializer.Serialize("not-a-date")})
            {
                var failed=false;
                try { JsonSerializer.Deserialize<CoverageRequest>("{\"status\":\"Approved\",\"evidence\":\"synthetic\",\"validUntil\":"+value+",\"version\":1}",options); }
                catch(JsonException) { failed=true; }
                assert(failed,"Reject unzoned or malformed coverage date.");
            }
        });
    }
}
