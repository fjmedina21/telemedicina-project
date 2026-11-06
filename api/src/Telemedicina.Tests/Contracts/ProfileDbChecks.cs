using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Telemedicina.Api.Features.Patients;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Tests;

// Explicit fixtures only. No deletion, ordinary accounts, credentials or payload output.
public static class ProfileDbChecks
{
    public static int Run()
    {
        var check = "SYNTHETIC_OPT_IN";
        try
        {
            if (Environment.GetEnvironmentVariable("MILESTONE_TEST_SYNTHETIC") != "1" ||
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development" ||
                Environment.GetEnvironmentVariable("Mail__Mode") != "development") throw new InvalidOperationException();
            var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var database = new Database(config);
            var id = "synthetic-h03-" + Guid.NewGuid().ToString("N"); var session = Guid.NewGuid().ToString("N");
            using (var c = database.Open())
            using (var t = c.BeginTransaction())
            {
                Execute(c,t,"INSERT INTO users(id,email,password_hash,role,verified) VALUES(@u,@email,'disabled-synthetic-login','Patient',true)",
                    ("u",id),("email",id+"@example.invalid"));
                Execute(c,t,"""
                    INSERT INTO sessions(id,user_id,access_hash,refresh_hash,access_expires,expires,family_id)
                    VALUES(@s,@u,@a,@r,@e,@e,@s)
                    """,("s",session),("u",id),("a",Guid.NewGuid().ToString("N")),("r",Guid.NewGuid().ToString("N")),
                    ("e",DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()));
                t.Commit();
            }
            PatientsController Controller(string user = "") => new(database)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] {new Claim(ClaimTypes.NameIdentifier,user.Length==0?id:user),
                        new Claim(ClaimTypes.Role,"Patient"), new Claim("session_id",session)},"synthetic"))
                } }
            };
            JsonElement Value(IActionResult result, int expected=200)
            {
                if(result is not ObjectResult obj || obj.StatusCode != expected) throw new InvalidOperationException();
                return JsonSerializer.SerializeToElement(obj.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            JsonElement Read() => Value(Controller().Profile()).GetProperty("data");
            var r = ProfileUnitChecks.Valid();
            check="ABSENT_PROFILE";
            if(Value(Controller().Completeness()).GetProperty("data").GetProperty("missingFields").GetArrayLength()!=10) throw new InvalidOperationException();
            check="CREATE_AND_METADATA"; Value(Controller().SaveProfile(r));
            var initial=Read(); var stamp=initial.GetProperty("declarationMetadata").GetProperty("allergies").GetProperty("updatedAt").GetString();
            if(initial.GetProperty("version").GetInt32()!=1 || stamp is null || initial.GetProperty("completeness").GetProperty("status").GetString()!="Complete") throw new InvalidOperationException();
            check="UNCHANGED_CATEGORY"; r=r with {Version=1,Phone="8095550103"}; Value(Controller().SaveProfile(r));
            if(Read().GetProperty("declarationMetadata").GetProperty("allergies").GetProperty("updatedAt").GetString()!=stamp) throw new InvalidOperationException();
            check="HISTORICAL_NULL_AND_MIGRATION_IDEMPOTENCE";
            using(var c=database.Open())
            using(var t=c.BeginTransaction())
            {
                Execute(c,t,"UPDATE profiles SET allergies_updated_at=NULL WHERE user_id=@u",("u",id));
                // Run the additive migration twice on historical fixture, roll it back after inspection.
                var path=Path.Combine(Directory.GetCurrentDirectory(),"api/src/Telemedicina.Api/Infrastructure/Persistence/Migrations/006_profile_declarations.sql");
                var sql=File.ReadAllText(path); Execute(c,t,sql); Execute(c,t,sql);
                if(Scalar(c,t,"SELECT allergies_updated_at FROM profiles WHERE user_id=@u",("u",id)) is not DBNull) throw new InvalidOperationException();
                t.Rollback();
                Execute(c,null,"UPDATE profiles SET allergies_updated_at=NULL WHERE user_id=@u",("u",id));
            }
            r=r with {Version=2,Phone="8095550104"}; Value(Controller().SaveProfile(r));
            if(Read().GetProperty("declarationMetadata").GetProperty("allergies").GetProperty("updatedAt").ValueKind!=JsonValueKind.Null) throw new InvalidOperationException();
            check="CLEAR_CATEGORY"; r=r with {Version=3,Allergies=[]}; Value(Controller().SaveProfile(r));
            if(Read().GetProperty("declarationMetadata").GetProperty("allergies").GetProperty("updatedAt").ValueKind!=JsonValueKind.String) throw new InvalidOperationException();
            check="IDENTITY_REJECTED_WITHOUT_WRITE";
            Value(Controller().SaveProfile(r with {Version=4,AdditionalProperties=new(){["patientId"]=JsonSerializer.SerializeToElement("foreign-synthetic")}}),400);
            if(Read().GetProperty("version").GetInt32()!=4) throw new InvalidOperationException();
            check="ISOLATION";
            if(Value(Controller("synthetic-h03-absent-"+Guid.NewGuid().ToString("N")).Profile()).GetProperty("data").ValueKind!=JsonValueKind.Null) throw new InvalidOperationException();
            check="CONCURRENT_VERSION_AND_AUDIT"; r=r with {Version=4};
            var statuses=Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>((ObjectResult)Controller().SaveProfile(r)).StatusCode))).GetAwaiter().GetResult();
            if(statuses.Count(s=>s==200)!=1 || statuses.Count(s=>s==409)!=1 || Read().GetProperty("version").GetInt32()!=5) throw new InvalidOperationException();
            using(var c=database.Open())
                if(Convert.ToInt64(Scalar(c,null,"SELECT count(*) FROM audit WHERE actor_id=@u AND action='profile.updated'",("u",id)))!=5) throw new InvalidOperationException();
            check="SESSION_REVOCATION";
            using(var c=database.Open()) Execute(c,null,"UPDATE sessions SET revoked=true WHERE id=@s",("s",session));
            var denied=false;
            try { Controller().SaveProfile(r with {Version=5}); } catch(Telemedicina.Api.Infrastructure.Authentication.SessionRejectedException e) {denied=e.Status==401;}
            if(!denied) throw new InvalidOperationException();
            Console.WriteLine("H03_DB_CHECKS_PASS: creation,metadata,historical,migration-twice,clear,identity,isolation,concurrency,audit,revocation");
            return 0;
        }
        catch {Console.Error.WriteLine("H03_DB_CHECKS_FAILED: "+check);return 1;}
    }
}
