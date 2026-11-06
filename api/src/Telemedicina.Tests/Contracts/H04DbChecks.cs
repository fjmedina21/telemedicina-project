using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Telemedicina.Api.Features.Appointments;
using Telemedicina.Api.Features.Catalog;
using Telemedicina.Api.Features.Notifications;
using Telemedicina.Api.Features.Scheduling;
using Telemedicina.Api.Infrastructure.Email;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Tests;

// Opt-in, exclusive synthetic fixtures. Never mutate ordinary accounts, audit history or global constraints.
public static class H04DbChecks
{
    public static int Run()
    {
        var stage="OPT_IN";
        try
        {
            if(Environment.GetEnvironmentVariable("MILESTONE_TEST_SYNTHETIC")!="1" ||
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!="Development" ||
                Environment.GetEnvironmentVariable("Mail__Mode")!="development") throw new InvalidOperationException();
            var config=new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var db=new Database(config);
            var environment=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development"}).Environment;
            var notifications=new NotificationService(new MailSettings(config,environment));
            var service=Guid.NewGuid().ToString(); var person=Guid.NewGuid().ToString();
            string Actor(string role)
            {
                var actor=Guid.NewGuid().ToString();
                using var c=db.Open();using var t=c.BeginTransaction();
                Execute(c,t,"INSERT INTO users(id,email,password_hash,role,verified) VALUES(@u,@email,'disabled-synthetic-login',@role,true)",
                    ("u",actor),("email","synthetic-h04-"+actor+"@example.invalid"),("role",role));
                Execute(c,t,"""
                    INSERT INTO sessions(id,user_id,access_hash,refresh_hash,access_expires,expires,family_id)
                    VALUES(@u,@u,@a,@r,@e,@e,@u)
                    """,("u",actor),("a",Guid.NewGuid().ToString("N")),("r",Guid.NewGuid().ToString("N")),
                    ("e",DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds()));
                if(role=="Patient") Execute(c,t,"INSERT INTO profiles(user_id,body,version) VALUES(@u,@body,1)",
                    ("u",actor),("body",JsonSerializer.Serialize(ProfileUnitChecks.Valid(),new JsonSerializerOptions(JsonSerializerDefaults.Web))));
                t.Commit();return actor;
            }
            var admin=Actor("Admission");var otherAdmin=Actor("Admission");var patient=Actor("Patient");
            ControllerContext Context(string actor,string role)
            {
                var context=new DefaultHttpContext();
                context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,actor),
                    new Claim(ClaimTypes.Role,role),new Claim("session_id",actor)},"synthetic"));
                context.Request.Headers["Idempotency-Key"]=Guid.NewGuid().ToString();
                return new ControllerContext{HttpContext=context};
            }
            SchedulesController Controller(string? actor=null)=>new(db){ControllerContext=Context(actor??admin,"Admission")};
            JsonElement Data(IActionResult result,int expected=200,string? expectedCode=null)
            {
                if(result is not ObjectResult value) { stage += "_NOT_OBJECT"; throw new InvalidOperationException(); }
                if(value.StatusCode!=expected) { stage += "_STATUS_" + value.StatusCode; throw new InvalidOperationException(); }
                stage += "_ENVELOPE";
                var root=JsonSerializer.SerializeToElement(value.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                stage += "_STATUS_FIELD";
                if(root.GetProperty("status").GetInt32()!=expected)
                    throw new InvalidOperationException();
                if(expected>=400)
                {
                    if(root.GetProperty("success").GetBoolean() ||
                        root.TryGetProperty("data",out _) ||
                        root.GetProperty("errors").ValueKind!=JsonValueKind.Array ||
                        string.IsNullOrWhiteSpace(expectedCode) ||
                        root.GetProperty("code").GetString()!=expectedCode)
                        throw new InvalidOperationException();
                    return root;
                }
                stage += "_DATA_FIELD";
                return root.GetProperty("data");
            }
            long CountAudit(string resource)
            {
                using var c=db.Open();return Convert.ToInt64(Scalar(c,null,"SELECT count(*) FROM audit WHERE resource_id=@r AND action='schedule.block_changed'",("r",resource)));
            }
            stage="FIXTURE";
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                Execute(c,t,"INSERT INTO services(id,specialty,name,duration,is_synthetic) VALUES(@s,'Synthetic H04','Synthetic H04',30,true)",("s",service));
                Execute(c,t,"INSERT INTO practitioners(id,name) VALUES(@p,'Synthetic H04')",("p",person));
                Execute(c,t,"INSERT INTO practitioner_services(practitioner_id,service_id) VALUES(@p,@s)",("p",person),("s",service));t.Commit();
            }
            var start=new DateTimeOffset(DateTime.UtcNow.Date.AddDays(2).AddHours(12),TimeSpan.Zero);
            JsonElement Add(DateTimeOffset at,string? actor=null)=>Data(Controller(actor).AddSlot(new(Guid.Parse(service),Guid.Parse(person),at)),201);
            var slot=Add(start).GetProperty("id").GetString()!;Add(start.AddHours(2));Add(start.AddHours(4));
            stage="PAGING_DATA";
            var page=Data(Controller().List(serviceId:service,limit:"1"));
            stage="PAGING_COUNT";
            if(page.GetProperty("totalCount").GetInt64()!=3 || !page.GetProperty("hasMore").GetBoolean())throw new InvalidOperationException();
            stage="PAGING_PRIVACY";
            var keys=page.GetProperty("items")[0].EnumerateObject().Select(p=>p.Name).ToHashSet();
            if(keys.Overlaps(new[]{"patientId","patientEmail","appointmentId","evidence"}))throw new InvalidOperationException();
            page=Data(Controller().List(serviceId:service,offset:"99",limit:"1"));
            if(page.GetProperty("totalCount").GetInt64()!=3 || page.GetProperty("items").GetArrayLength()!=0)throw new InvalidOperationException();
            stage="BLOCK_VERSION_NOOP_LEGACY";
            Data(Controller().Block(Guid.Parse(slot),new(true,1)));
            var unchanged=Data(Controller().Block(Guid.Parse(slot),new(true,2)));
            if(unchanged.GetProperty("version").GetInt32()!=2 || CountAudit(slot)!=1)throw new InvalidOperationException();
            Data(Controller().Block(Guid.Parse(slot),new(false,1)),409,"VERSION_CONFLICT");
            Data(Controller().Block(Guid.Parse(slot),new(false)));
            if(CountAudit(slot)!=2)throw new InvalidOperationException();
            stage="MIGRATION_TWICE";
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                var before=Rows(c,t,"SELECT id,blocked,version,start_utc,end_utc FROM slots WHERE id=@s",("s",slot)).Single();
                var sql=File.ReadAllText("api/src/Telemedicina.Api/Infrastructure/Persistence/Migrations/007_slot_versions.sql");
                Execute(c,t,sql);Execute(c,t,sql);
                var after=Rows(c,t,"SELECT id,blocked,version,start_utc,end_utc FROM slots WHERE id=@s",("s",slot)).Single();
                if(before.Any(p=>!Equals(p.Value,after[p.Key])))throw new InvalidOperationException();t.Rollback();
            }
            stage="CONCURRENT_OVERLAP";
            var at=start.AddHours(6);
            var statuses=Task.WhenAll(new[]{admin,otherAdmin}.Select(actor=>Task.Run(()=>
                ((ObjectResult)Controller(actor).AddSlot(new(Guid.Parse(service),Guid.Parse(person),at))).StatusCode))).GetAwaiter().GetResult();
            if(statuses.Count(v=>v==201)!=1 || statuses.Count(v=>v==409)!=1)throw new InvalidOperationException();
            using(var c=db.Open())
                if(Convert.ToInt64(Scalar(c,null,"SELECT count(*) FROM slots WHERE service_id=@s AND start_utc=@at",("s",service),("at",at.UtcDateTime)))!=1)throw new InvalidOperationException();
            stage="CLOCK_AFTER_OFFER_LOCK";
            var clockPatient=Actor("Patient");
            var nearStart=DateTimeOffset.UtcNow.AddSeconds(8);
            var nearSlot=Add(nearStart).GetProperty("id").GetString()!;
            var clockController=new AppointmentsController(db,notifications)
                {ControllerContext=Context(clockPatient,"Patient")};
            Task<IActionResult> clockRequest;
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                Scalar(c,t,"SELECT id FROM services WHERE id=@s FOR UPDATE",("s",service));
                var blocker=Convert.ToInt32(Scalar(c,t,"SELECT pg_backend_pid()"));
                clockRequest=Task.Run(()=>clockController.Book(new(Guid.Parse(nearSlot),"Synthetic H04","0000")));
                var waiting=false;
                while(DateTimeOffset.UtcNow<nearStart.AddMilliseconds(-100))
                {
                    waiting=(bool)Scalar(c,t,"""
                        SELECT EXISTS(
                            SELECT 1 FROM pg_locks
                            WHERE NOT granted
                            AND @blocker=ANY(pg_blocking_pids(pid)))
                        """,("blocker",blocker))!;
                    if(waiting)break;
                    if(clockRequest.IsCompleted)throw new InvalidOperationException();
                    Thread.Sleep(20);
                }
                if(!waiting || DateTimeOffset.UtcNow>=nearStart)
                    throw new InvalidOperationException();
                while(DateTimeOffset.UtcNow<=nearStart)Thread.Sleep(20);
                t.Rollback();
            }
            Data(clockRequest.GetAwaiter().GetResult(),409,"SLOT_UNAVAILABLE");
            using(var c=db.Open())
            {
                foreach(var table in new[]{"appointments","idempotency","notifications","email_outbox"})
                    if(Convert.ToInt64(Scalar(c,null,
                        "SELECT count(*) FROM "+table+" WHERE user_id=@u",("u",clockPatient)))!=0)
                        throw new InvalidOperationException();
            }
            stage="CONCURRENT_VERSION";
            statuses=Task.WhenAll(new[]{admin,otherAdmin}.Select(actor=>Task.Run(()=>
                ((ObjectResult)Controller(actor).Block(Guid.Parse(slot),new(true,3))).StatusCode))).GetAwaiter().GetResult();
            if(statuses.Count(v=>v==200)!=1 || statuses.Count(v=>v==409)!=1 || CountAudit(slot)!=3)throw new InvalidOperationException();
            stage="RESERVATION_AND_GUARD";
            Data(Controller().Block(Guid.Parse(slot),new(false,4)));
            var appointmentController=new AppointmentsController(db,notifications){ControllerContext=Context(patient,"Patient")};
            var booked=Data(appointmentController.Book(new(Guid.Parse(slot),"Synthetic H04","0000")),201);
            Data(Controller().Block(Guid.Parse(slot),new(true,5)),409,"SLOT_RESERVED");
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                OfferPolicy.LockServices(c,t,[service]);
                Scalar(c,t,"SELECT id FROM practitioners WHERE id=@p FOR UPDATE",("p",person));
                Execute(c,t,"UPDATE practitioner_services SET active=false WHERE service_id=@s AND practitioner_id=@p",("s",service),("p",person));
                var pending=Task.Run(()=>Controller(otherAdmin).AddSlot(new(Guid.Parse(service),Guid.Parse(person),start.AddHours(8))));
                Thread.Sleep(150);t.Commit();
                Data(pending.GetAwaiter().GetResult(),400,"PRACTITIONER_SERVICE_REQUIRED");
            }
            using(var c=db.Open())
                if((string?)Scalar(c,null,"SELECT status FROM appointments WHERE id=@a",("a",booked.GetProperty("id").GetString()))!="PendingValidation")throw new InvalidOperationException();
            stage="HISTORICAL_PUBLICATION_ISOLATED";
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                // Temporary shadow tables deliberately lack global publication constraints; no real table is changed.
                Execute(c,t,"""
                    CREATE TEMP TABLE services(id text,active boolean,is_synthetic boolean,approval_reference text,duration integer) ON COMMIT DROP;
                    CREATE TEMP TABLE practitioners(id text,active boolean) ON COMMIT DROP;
                    CREATE TEMP TABLE practitioner_services(service_id text,practitioner_id text,active boolean) ON COMMIT DROP;
                    INSERT INTO services VALUES('legacy',true,false,NULL,30);
                    INSERT INTO practitioners VALUES('synthetic',true);
                    INSERT INTO practitioner_services VALUES('legacy','synthetic',true);
                    """);
                var offer=OfferPolicy.Read(c,t,"legacy","synthetic");
                if(offer is null || offer.Published || !offer.Assigned ||
                    Convert.ToInt64(Scalar(c,t,"SELECT count(*) FROM services v WHERE "+OfferPolicy.PublicServiceSql))!=0)throw new InvalidOperationException();
                t.Rollback();
            }
            stage="REVOKED_SESSION";
            using(var c=db.Open())Execute(c,null,"UPDATE sessions SET revoked=true WHERE id=@s",("s",admin));
            var denied=false;
            try{Controller().Block(Guid.Parse(slot),new(false));}
            catch(Telemedicina.Api.Infrastructure.Authentication.SessionRejectedException e){denied=e.Status==401;}
            if(!denied)throw new InvalidOperationException();
            Console.WriteLine("H04_DB_CHECKS_PASS: paging,privacy,version,no-op,legacy,migration-twice,overlap,clock-after-locks,reservation,association-race,historical-publication,revocation");return 0;
        }
        catch(Npgsql.PostgresException error){Console.Error.WriteLine("H04_DB_CHECKS_FAILED: "+stage+" SQLSTATE="+error.SqlState);return 1;}
        catch{Console.Error.WriteLine("H04_DB_CHECKS_FAILED: "+stage);return 1;}
    }
}
