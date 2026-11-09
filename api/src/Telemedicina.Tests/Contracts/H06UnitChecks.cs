using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Telemedicina.Api.Features.Appointments;

namespace Telemedicina.Tests;

public static class H06UnitChecks
{
    public static void Register(Action<string,Action> test, Action<bool,string> assert)
    {
        var start=new DateTime(2040,1,1,15,0,0,DateTimeKind.Utc);
        test("H06_WindowAndProgressStates",()=>
        {
            assert(CheckInPolicy.OpensAt(start)==start.AddMinutes(-30) &&
                CheckInPolicy.ClosesAt(start)==start.AddMinutes(15),"Exact -30/+15 window.");
            assert(CheckInPolicy.State("Confirmed",start,start,true,false,false,false,false,false)=="NotStarted","Empty attempt.");
            assert(CheckInPolicy.State("Confirmed",start,start,true,true,false,false,false,false)=="InProgress","Partial progress.");
            assert(CheckInPolicy.State("Confirmed",start,start,true,true,true,true,true,false)=="Verified","Administrative verification.");
            assert(CheckInPolicy.State("Confirmed",start,start,true,true,true,true,true,true)=="Ready","Server-derived Ready.");
            assert(CheckInPolicy.State("Cancelled",start,start,true,true,true,true,true,true)=="Rejected","Cancellation rejects.");
            assert(CheckInPolicy.State("Confirmed",start,start.AddMinutes(16),true,true,true,true,true,true)=="Expired","Late attempt expires.");
        });
        test("H06_AdultSelfIsExplicit",()=>
        {
            var today=new DateOnly(2040,1,1);
            assert(CheckInPolicy.IsAdultSelf(JsonSerializer.Deserialize<JsonElement>("""{"birthDate":"2022-01-01"}"""),today),"Exactly eighteen is adult.");
            assert(!CheckInPolicy.IsAdultSelf(JsonSerializer.Deserialize<JsonElement>("""{"birthDate":"2022-01-02"}"""),today),"Minor is blocked.");
            assert(!CheckInPolicy.IsAdultSelf(JsonSerializer.Deserialize<JsonElement>("{}"),today),"Missing age is blocked.");
        });
        test("H06_ConsentFingerprintIsExact",()=>assert(CheckInPolicy.ConsentText==
            "Demostración académica: no constituye atención médica real y no debe utilizarse para emergencias." &&
            CheckInPolicy.ConsentHash=="a7ae0d686693ad80daaef8b775d47b062484ad354ecb17a90df10ac865626631","Exact approved consent."));
        test("H06_401_403_MetadataAndAdmissionRedaction",()=>
        {
            var type=typeof(CheckInController);
            assert(type.GetCustomAttribute<AuthorizeAttribute>() is not null,"Controller requires authentication (401 contract).");
            string Role(string method)=>type.GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles!;
            assert(Role(nameof(CheckInController.PatientRequirements))=="Patient" && Role(nameof(CheckInController.Submit))=="Patient","Admission is forbidden from patient declarations.");
            assert(Role(nameof(CheckInController.AdmissionStatus))=="Admission","Patient is forbidden from admission status endpoint.");
            var json=JsonSerializer.Serialize(new AdmissionCheckInStatus("synthetic","Ready",1,2,start,start,null,start),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            foreach(var forbidden in new[]{"prepared","privacy","connectivity","preflight","consent","hash","text"})
                assert(!json.Contains(forbidden,StringComparison.OrdinalIgnoreCase),"Admission DTO redacts "+forbidden);
        });
    }
}
