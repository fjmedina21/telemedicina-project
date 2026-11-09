using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;
using System.Text.Json;
using Telemedicina.Api.Features.Appointments;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Tests;

public static class H06DbChecks
{
    public static int Run()
    {
        var stage="OPT_IN";
        try
        {
            if(Environment.GetEnvironmentVariable("MILESTONE_TEST_SYNTHETIC")!="1" ||
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development")
                throw new InvalidOperationException();
            var db=new Database(new ConfigurationBuilder().AddEnvironmentVariables().Build());
            stage="MIGRATION_RESET_MANIFEST";
            using(var c=db.Open())
            {
                if(Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM schema_versions WHERE version=10"))!=1) throw new InvalidOperationException();
                if(Convert.ToInt32(Scalar(c,null,"SELECT count(*) FROM pg_views WHERE schemaname='telemedicina_m1' AND viewname='ready_check_ins'"))!=1) throw new InvalidOperationException();
            }
            stage="CONCURRENT_ACTIVE_ATTEMPT";
            var user=Guid.NewGuid().ToString();var service=Guid.NewGuid().ToString();
            var practitioner=Guid.NewGuid().ToString();var slot=Guid.NewGuid().ToString();var appointment=Guid.NewGuid().ToString();
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                Execute(c,t,"INSERT INTO users(id,email,password_hash,role,verified) VALUES(@id,@email,'synthetic-disabled','Patient',true)",("id",user),("email",user+"@example.invalid"));
                Execute(c,t,"INSERT INTO profiles(user_id,body,version) VALUES(@u,@body,1)",
                    ("u",user),("body",JsonSerializer.Serialize(ProfileUnitChecks.Valid(),new JsonSerializerOptions(JsonSerializerDefaults.Web))));
                Execute(c,t,"INSERT INTO sessions(id,user_id,access_hash,refresh_hash,access_expires,expires,family_id) VALUES(@id,@u,@a,@r,@end,@end,@id)",
                    ("id",user),("u",user),("a",Guid.NewGuid().ToString()),("r",Guid.NewGuid().ToString()),
                    ("end",DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()));
                Execute(c,t,"INSERT INTO services(id,specialty,name,duration) VALUES(@id,'Synthetic','Synthetic',30)",("id",service));
                Execute(c,t,"INSERT INTO practitioners(id,name) VALUES(@id,'Synthetic')",("id",practitioner));
                var start=DateTime.UtcNow;
                Execute(c,t,"INSERT INTO slots(id,service_id,practitioner_id,start_utc,end_utc) VALUES(@id,@s,@p,@start,@end)",("id",slot),("s",service),("p",practitioner),("start",start),("end",start.AddMinutes(30)));
                Execute(c,t,"INSERT INTO appointments(id,user_id,slot_id,practitioner_id,start_utc,end_utc,status,coverage,insurer,member_last4) VALUES(@id,@u,@slot,@p,@start,@end,'Confirmed','Approved','Synthetic','0000')",("id",appointment),("u",user),("slot",slot),("p",practitioner),("start",start),("end",start.AddMinutes(30)));
                t.Commit();
            }
            stage="NOT_FOUND";
            var http=new DefaultHttpContext();
            http.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,user),
                new Claim(ClaimTypes.Role,"Patient"),new Claim("session_id",user)},"synthetic"));
            var controller=new CheckInController(db){ControllerContext=new ControllerContext{HttpContext=http}};
            if(controller.PatientRequirements(Guid.NewGuid()) is not NotFoundResult) throw new InvalidOperationException();
            var results=Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>
            {
                try { using var c=db.Open();Execute(c,null,"INSERT INTO check_in_attempts(id,appointment_id,user_id,appointment_version,profile_version,state) VALUES(@id,@a,@u,1,1,'NotStarted')",("id",Guid.NewGuid().ToString()),("a",appointment),("u",user));return "OK"; }
                catch(PostgresException e){return e.SqlState;}
            }))).WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            if(!results.Order().SequenceEqual(new[]{"23505","OK"})) throw new InvalidOperationException();
            stage="READY_DB_GUARDS";
            var rejectedReady=false;
            try { using var c=db.Open();Execute(c,null,"UPDATE check_in_attempts SET state='Ready' WHERE appointment_id=@a",("a",appointment)); }
            catch(PostgresException e) when(e.SqlState=="23514") { rejectedReady=true; }
            if(!rejectedReady) throw new InvalidOperationException();
            stage="INVALIDATION_AND_READY_VIEW";
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                Execute(c,t,"""
                    UPDATE check_in_attempts SET state='Ready',prepared=true,privacy_confirmed=true,
                        connectivity_confirmed=true,technical_preflight_completed=true,
                        consent_version=1,consent_hash=@hash,ready_at=now()
                    WHERE appointment_id=@a
                    """,("hash",CheckInPolicy.ConsentHash),("a",appointment));
                if(Convert.ToInt32(Scalar(c,t,"SELECT count(*) FROM ready_check_ins WHERE appointment_id=@a",("a",appointment)))!=1) throw new InvalidOperationException();
                Execute(c,t,"UPDATE check_in_attempts SET active=false,state='Rejected',ready_at=NULL,invalidated_at=now() WHERE appointment_id=@a",("a",appointment));
                if(Convert.ToInt32(Scalar(c,t,"SELECT count(*) FROM check_in_attempts WHERE appointment_id=@a AND active",("a",appointment)))!=0) throw new InvalidOperationException();
                t.Rollback();
            }
            stage="IDEMPOTENT_REPLAY_INVALIDATED";
            var key=Guid.NewGuid().ToString();http.Request.Headers["Idempotency-Key"]=key;
            var submitted=controller.Submit(Guid.Parse(appointment),new CheckInRequest(1,true,true,true,true,1,true));
            if(submitted is not ObjectResult { StatusCode:200 }) throw new InvalidOperationException();
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                Execute(c,t,"UPDATE check_in_attempts SET active=false,state='Rejected',ready_at=NULL,invalidated_at=now() WHERE appointment_id=@a",("a",appointment));
                Execute(c,t,"UPDATE appointments SET status='Cancelled',version=version+1 WHERE id=@a",("a",appointment));
                t.Commit();
            }
            var replay=controller.Submit(Guid.Parse(appointment),new CheckInRequest(1,true,true,true,true,1,true));
            if(replay is not ObjectResult { StatusCode:409 } conflict ||
                JsonSerializer.SerializeToElement(conflict.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    .GetProperty("code").GetString()!="CHECK_IN_INVALIDATED") throw new InvalidOperationException();
            Console.WriteLine("H06_DB_PASS: migration,reset-manifest,concurrency,invalidation,ready-view");return 0;
        }
        catch { Console.Error.WriteLine("H06_DB_FAILED:"+stage);return 1; }
    }
}
