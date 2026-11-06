using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Telemedicina.Api.Features.Admission;
using Telemedicina.Api.Features.Appointments;
using Telemedicina.Api.Features.Notifications;
using Telemedicina.Api.Infrastructure.Email;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared.Contracts;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;

namespace Telemedicina.Tests;

/// <summary>Opt-in synthetic fixtures only; no global trigger/table changes, destructive cleanup, or real transport.</summary>
public static class H05DbChecks
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
            var env=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Development"}).Environment;
            var notifications=new NotificationService(new MailSettings(config,env));
            var json=new JsonSerializerOptions(JsonSerializerDefaults.Web);
            void Require(bool value) { if(!value) throw new InvalidOperationException(); }
            JsonElement Data(IActionResult result,int status=200,string? code=null)
            {
                Require(result is ObjectResult);
                var value=(ObjectResult)result;
                Require(value.StatusCode==status);
                var root=JsonSerializer.SerializeToElement(value.Value,json);
                if(status>=400) { Require(root.GetProperty("code").GetString()==code);return root; }
                return root.GetProperty("data");
            }
            string Actor(string role="Patient",string? body="complete")
            {
                var id=Guid.NewGuid().ToString();
                using var c=db.Open();using var t=c.BeginTransaction();
                Execute(c,t,"INSERT INTO users(id,email,password_hash,role,verified) VALUES(@u,@e,'disabled-synthetic-login',@role,true)",
                    ("u",id),("e","synthetic-h05-"+id+"@example.invalid"),("role",role));
                Execute(c,t,"""
                    INSERT INTO sessions(id,user_id,access_hash,refresh_hash,access_expires,expires,family_id)
                    VALUES(@u,@u,@a,@r,@end,@end,@u)
                    """,("u",id),("a",Guid.NewGuid().ToString()),("r",Guid.NewGuid().ToString()),
                    ("end",DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()));
                if(role=="Patient" && body is not null)
                    Execute(c,t,"INSERT INTO profiles(user_id,body,version) VALUES(@u,@b,1)",("u",id),
                        ("b",body=="complete"?JsonSerializer.Serialize(ProfileUnitChecks.Valid(),json):body));
                Execute(c,t,"INSERT INTO notification_preferences(user_id,email_enabled) VALUES(@u,true)",("u",id));
                t.Commit();return id;
            }
            ControllerContext Context(string actor,string role,string? key=null)
            {
                var http=new DefaultHttpContext();
                http.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,actor),
                    new Claim(ClaimTypes.Role,role),new Claim("session_id",actor)},"synthetic"));
                http.Request.Headers["Idempotency-Key"]=key??Guid.NewGuid().ToString();
                return new ControllerContext{HttpContext=http};
            }
            AppointmentsController Patient(string actor,string? key=null,NotificationService? delivery=null)=>
                new(db,delivery??notifications){ControllerContext=Context(actor,"Patient",key)};
            var admission=Actor("Admission");
            AdmissionController Staff()=>new(db,notifications){ControllerContext=Context(admission,"Admission")};
            var patient=Actor();var other=Actor();
            var service=Guid.NewGuid().ToString();var otherService=Guid.NewGuid().ToString();
            var practitioner=Guid.NewGuid().ToString();var otherPractitioner=Guid.NewGuid().ToString();
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                foreach(var s in new[]{service,otherService})
                    Execute(c,t,"INSERT INTO services(id,name,specialty,duration,is_synthetic) VALUES(@s,'Synthetic H05','Synthetic H05',30,true)",("s",s));
                foreach(var p in new[]{practitioner,otherPractitioner})
                {
                    Execute(c,t,"INSERT INTO practitioners(id,name) VALUES(@p,'Synthetic H05')",("p",p));
                    foreach(var s in new[]{service,otherService})
                        Execute(c,t,"INSERT INTO practitioner_services(practitioner_id,service_id) VALUES(@p,@s)",("p",p),("s",s));
                }
                t.Commit();
            }
            var clock=DateTime.UtcNow.Date.AddDays(10);
            var nextHour=0;
            string Slot(string? s=null,string? p=null)
            {
                var id=Guid.NewGuid().ToString();var start=clock.AddHours(nextHour++);
                using var c=db.Open();
                Execute(c,null,"INSERT INTO slots(id,service_id,practitioner_id,start_utc,end_utc) VALUES(@id,@s,@p,@start,@end)",
                    ("id",id),("s",s??service),("p",p??practitioner),("start",start),("end",start.AddMinutes(30)));
                return id;
            }
            string Book(string actor,string slot)
                =>Data(Patient(actor).Book(new(Guid.Parse(slot),"Synthetic H05","0000")),201).GetProperty("id").GetString()!;
            int Status(IActionResult r)=>((ObjectResult)r).StatusCode!.Value;
            long Count(string table,string appointment)
            {
                using var c=db.Open();
                var column=table=="appointment_history"?"appointment_id":"resource_id";
                return Convert.ToInt64(Scalar(c,null,"SELECT count(*) FROM "+table+" WHERE "+column+"=@a",("a",appointment)));
            }
            string Snapshot(string appointment)
            {
                using var c=db.Open();
                return JsonSerializer.Serialize(Rows(c,null,"SELECT * FROM appointments WHERE id=@a",("a",appointment)),json)+
                    ":"+Count("appointment_history",appointment)+":"+Count("notifications",appointment)+":"+Count("email_outbox",appointment);
            }
            JsonElement Detail(string appointment,string actor)=>Data(Patient(actor).Detail(Guid.Parse(appointment)));
            int Version(string appointment,string actor)=>Detail(appointment,actor).GetProperty("version").GetInt32();

            stage="PROFILE_AND_LEGACY_REPLAY";
            var absent=Actor(body:null);var incomplete=Actor(body:"{}");var slot=Slot();
            Data(Patient(absent).Book(new(Guid.Parse(slot),"Synthetic","0000")),400,"PROFILE_REQUIRED");
            var missing=Data(Patient(incomplete).Book(new(Guid.Parse(slot),"Synthetic","0000")),400,"PROFILE_INCOMPLETE");
            Require(missing.GetProperty("errors").GetArrayLength()==10);
            foreach(var actor in new[]{absent,incomplete})
            {
                using var c=db.Open();
                foreach(var table in new[]{"appointments","idempotency","notifications","email_outbox"})
                    Require(Convert.ToInt64(Scalar(c,null,"SELECT count(*) FROM "+table+" WHERE user_id=@u",("u",actor)))==0);
            }
            var literalKey="{"+Guid.NewGuid().ToString().ToUpperInvariant()+"}";
            var body=new BookRequest(Guid.Parse(slot)," Synthetic H05 ","0000");
            var created=Data(Patient(patient,literalKey).Book(body),201);
            var appointment=created.GetProperty("id").GetString()!;
            var before=Snapshot(appointment);
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                Require((string?)Scalar(c,t,"SELECT request_hash FROM idempotency WHERE user_id=@u AND key=@key",
                    ("u",patient),("key",literalKey))==Hash(JsonSerializer.Serialize(body,json)));
                Execute(c,t,"UPDATE profiles SET body='{}' WHERE user_id=@u",("u",patient));
                Execute(c,t,"UPDATE services SET active=false WHERE id=@s",("s",service));
                Execute(c,t,"UPDATE practitioner_services SET active=false WHERE service_id=@s",("s",service));
                t.Commit();
            }
            Require(Data(Patient(patient,literalKey).Book(body)).GetProperty("id").GetString()==appointment);
            Data(Patient(patient,literalKey).Book(body with{Insurer="different"}),409,"IDEMPOTENCY_MISMATCH");
            Data(Patient(patient,Guid.Parse(literalKey).ToString()).Book(body),400,"PROFILE_INCOMPLETE");
            Require(Snapshot(appointment)==before);
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                Execute(c,t,"UPDATE profiles SET body=@b WHERE user_id=@u",("b",JsonSerializer.Serialize(ProfileUnitChecks.Valid(),json)),("u",patient));
                Execute(c,t,"UPDATE services SET active=true WHERE id=@s",("s",service));
                Execute(c,t,"UPDATE practitioner_services SET active=true WHERE service_id=@s",("s",service));t.Commit();
            }
            Data(Patient(other,literalKey).Book(body),409,"SLOT_UNAVAILABLE");

            stage="SAME_SLOT_AND_SERVICE";
            before=Snapshot(appointment);
            Data(Patient(patient).Reschedule(Guid.Parse(appointment),new(Guid.Parse(slot),1)),409,"SAME_SLOT");
            var wrong=Slot(otherService);
            Data(Patient(patient).Reschedule(Guid.Parse(appointment),new(Guid.Parse(wrong),1)),409,"SERVICE_CHANGE_NOT_ALLOWED");
            Require(Snapshot(appointment)==before);

            stage="COVERAGE_BOUNDARIES_AND_CLEARING";
            var end=DateTime.SpecifyKind(Detail(appointment,patient).GetProperty("endAt").GetDateTime(),DateTimeKind.Utc);
            Data(Staff().Review(Guid.Parse(appointment),new("Approved","synthetic",new DateTimeOffset(end.AddTicks(-1)),1)),400,"EVIDENCE_REQUIRED");
            Data(Staff().Review(Guid.Parse(appointment),new("Approved","synthetic",DateTimeOffset.UtcNow,1)),400,"EVIDENCE_REQUIRED");
            Data(Staff().Review(Guid.Parse(appointment),new("Approved"," ",new DateTimeOffset(end),1)),400,"EVIDENCE_REQUIRED");
            Require(Snapshot(appointment)==before);
            Data(Staff().Review(Guid.Parse(appointment),new("Approved"," synthetic ",new DateTimeOffset(end),1)));
            var detail=Detail(appointment,patient);
            Require(detail.GetProperty("coverageStatus").GetString()=="Approved" &&
                detail.GetProperty("coverageMethod").GetString()=="Manual" && !detail.GetProperty("coverageRequiresReview").GetBoolean());
            Data(Staff().Review(Guid.Parse(appointment),new("Denied","unfavorable",new DateTimeOffset(end.AddDays(1)),2)));
            detail=Detail(appointment,patient);
            Require(detail.GetProperty("status").GetString()=="PendingValidation" && detail.GetProperty("validUntil").ValueKind==JsonValueKind.Null);
            Data(Patient(other).Book(new(Guid.Parse(slot),"Synthetic","0000")),409,"SLOT_UNAVAILABLE");
            Data(Staff().Review(Guid.Parse(appointment),new("ManualReview","pending",new DateTimeOffset(end.AddDays(1)),3)));
            Require(Detail(appointment,patient).GetProperty("validUntil").ValueKind==JsonValueKind.Null);
            Data(Staff().Review(Guid.Parse(appointment),new("Approved","favorable",new DateTimeOffset(end),4)));
            var newSlot=Slot(p:otherPractitioner);
            Data(Patient(patient).Reschedule(Guid.Parse(appointment),new(Guid.Parse(newSlot),5)));
            detail=Detail(appointment,patient);
            Require(detail.GetProperty("practitionerId").GetString()==otherPractitioner && detail.GetProperty("version").GetInt32()==6);
            foreach(var name in new[]{"evidence","reviewedBy","reviewedAt","validUntil"})
                Require(detail.GetProperty(name).ValueKind==JsonValueKind.Null);
            Require(detail.GetProperty("coverageStatus").GetString()=="ManualReview");
            using(var c=db.Open())
                Require((string?)Scalar(c,null,"SELECT old_evidence FROM appointment_history WHERE appointment_id=@a AND version=6",("a",appointment))=="favorable");

            stage="EFFECTIVE_COVERAGE_READ_ONLY";
            foreach(var test in new[]{
                (Until:(DateTime?)null,Status:"ManualReview",Reason:"MissingValidity"),
                (Until:(DateTime?)DateTime.UtcNow.AddSeconds(-1),Status:"Expired",Reason:"Expired"),
                (Until:(DateTime?)DateTime.UtcNow.AddDays(1),Status:"ManualReview",Reason:"OutsideAppointment")})
            {
                using(var c=db.Open())Execute(c,null,"UPDATE appointments SET coverage='Approved',coverage_valid_until=@until::timestamptz WHERE id=@a",
                    ("until",test.Until),("a",appointment));
                before=Snapshot(appointment);
                detail=Detail(appointment,patient);
                Require(detail.GetProperty("coverageStatus").GetString()==test.Status &&
                    detail.GetProperty("coverageValidityReason").GetString()==test.Reason &&
                    detail.GetProperty("coverageRequiresReview").GetBoolean());
                Data(Patient(patient).Mine(coverageStatus:test.Status));
                Data(Staff().Queue(coverageStatus:test.Status));
                Require(Snapshot(appointment)==before);
            }

            stage="CONCURRENT_BOOK_AND_CANCEL";
            var contested=Slot();
            var bookings=Task.WhenAll(new[]{patient,other}.Select(actor=>Task.Run(()=>
                (Actor:actor,Result:Patient(actor).Book(new(Guid.Parse(contested),"Synthetic","0000"))))))
                .WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();
            Require(bookings.Select(x=>Status(x.Result)).Order().SequenceEqual(new[]{201,409}));
            var winner=bookings.Single(x=>Status(x.Result)==201);
            var bookedId=Data(winner.Result,201).GetProperty("id").GetString()!;
            var cancellations=Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(()=>Patient(winner.Actor).Cancel(Guid.Parse(bookedId),new VersionRequest(1)))))
                .WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();
            Require(cancellations.Select(Status).Order().SequenceEqual(new[]{200,409}));
            Require(Count("appointment_history",bookedId)==2 && Count("notifications",bookedId)==2 && Count("email_outbox",bookedId)==2);

            stage="OCCUPIED_SWAP_NO_CYCLE";
            var leftSlot=Slot();var rightSlot=Slot();
            var left=Book(patient,leftSlot);var right=Book(other,rightSlot);
            var swapped=Task.WhenAll(
                Task.Run(()=>Patient(patient).Reschedule(Guid.Parse(left),new(Guid.Parse(rightSlot),1))),
                Task.Run(()=>Patient(other).Reschedule(Guid.Parse(right),new(Guid.Parse(leftSlot),1))))
                .WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();
            foreach(var result in swapped)Data(result,409,"SLOT_UNAVAILABLE");
            Require(Version(left,patient)==1 && Version(right,other)==1);

            stage="REVIEW_CANCEL_AND_RESCHEDULE_RACES";
            var race=Book(patient,Slot());
            var raceEnd=Detail(race,patient).GetProperty("endAt").GetDateTime();
            var changes=Task.WhenAll(
                Task.Run(()=>Staff().Review(Guid.Parse(race),new("Approved","synthetic",new DateTimeOffset(raceEnd),1))),
                Task.Run(()=>Patient(patient).Cancel(Guid.Parse(race),new VersionRequest(1))))
                .WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();
            Require(changes.Select(Status).Order().SequenceEqual(new[]{200,409}));
            Require(Count("appointment_history",race)==2 && Count("notifications",race)==2 && Version(race,patient)==2);
            var race2=Book(patient,Slot());var targetSlot=Slot();
            var changes2=Task.WhenAll(
                Task.Run(()=>Patient(patient).Reschedule(Guid.Parse(race2),new(Guid.Parse(targetSlot),1))),
                Task.Run(()=>Patient(patient).Cancel(Guid.Parse(race2),new VersionRequest(1))))
                .WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();
            Require(changes2.Select(Status).Order().SequenceEqual(new[]{200,409}));
            Require(Count("appointment_history",race2)==2 && Count("notifications",race2)==2 && Version(race2,patient)==2);

            stage="POST_MUTATION_OUTBOX_FAILURE_ROLLS_BACK";
            var rollbackActor=Actor();var rollbackSlot=Slot();
            var brokenConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
                {["Mail:Mode"]="smtp",["Mail:PayloadKey"]="invalid-synthetic-key"}).Build();
            // NotificationService has no transport. This fails encryption after DML, before commit; no SMTP is called.
            var broken=new NotificationService(new MailSettings(brokenConfig,env));
            var failed=false;
            try { Patient(rollbackActor,delivery:broken).Book(new(Guid.Parse(rollbackSlot),"Synthetic","0000")); }
            catch(FormatException) { failed=true; }
            Require(failed);
            using(var c=db.Open())
            {
                foreach(var table in new[]{"appointments","idempotency","notifications","email_outbox"})
                    Require(Convert.ToInt64(Scalar(c,null,"SELECT count(*) FROM "+table+" WHERE user_id=@u",("u",rollbackActor)))==0);
                Require(Convert.ToInt64(Scalar(c,null,"SELECT count(*) FROM appointment_history h JOIN slots s ON s.id=h.new_slot_id WHERE s.id=@s",
                    ("s",rollbackSlot)))==0);
                Require(Convert.ToInt64(Scalar(c,null,"SELECT count(*) FROM audit WHERE actor_id=@u",("u",rollbackActor)))==0);
            }

            stage="EFFECTIVE_FILTER_BEFORE_LIMIT";
            var filterActor=Actor();var rangeStart=DateTime.UtcNow.AddYears(15);var prefix=Guid.NewGuid().ToString();
            using(var c=db.Open())using(var t=c.BeginTransaction())
            {
                Execute(c,t,"""
                    INSERT INTO slots(id,service_id,practitioner_id,start_utc,end_utc)
                    SELECT md5(@prefix||i::text)::uuid::text,@s,@p,@start::timestamptz+i*interval '1 hour',
                        @start::timestamptz+i*interval '1 hour'+interval '30 minutes'
                    FROM generate_series(0,204) i;
                    INSERT INTO appointments(id,user_id,slot_id,practitioner_id,start_utc,end_utc,status,coverage,insurer,member_last4,coverage_valid_until)
                    SELECT md5(s.id||'appointment')::uuid::text,@u,s.id,@p,s.start_utc,s.end_utc,'PendingValidation',
                        CASE WHEN s.start_utc=@start::timestamptz THEN 'Approved' ELSE 'ManualReview' END,
                        'Synthetic H05','0000',CASE WHEN s.start_utc=@start::timestamptz THEN @past::timestamptz ELSE NULL END
                    FROM slots s WHERE s.practitioner_id=@p AND s.start_utc>=@start::timestamptz AND s.start_utc<@end::timestamptz;
                    """,("prefix",prefix),("s",service),("p",otherPractitioner),("u",filterActor),
                    ("start",rangeStart),("end",rangeStart.AddHours(205)),("past",DateTime.UtcNow.AddMinutes(-1)));
                t.Commit();
            }
            Require(Data(Patient(filterActor).Mine()).GetArrayLength()==100);
            var filtered=Data(Patient(filterActor).Mine(rangeStart.ToString("O"),rangeStart.AddHours(205).ToString("O"),null,"Expired"));
            Require(filtered.GetArrayLength()==1 && filtered[0].GetProperty("coverageStatus").GetString()=="Expired");
            Require(Data(Staff().Queue(rangeStart.ToString("O"),rangeStart.AddHours(205).ToString("O"))).GetArrayLength()==200);
            var queue=Data(Staff().Queue(rangeStart.ToString("O"),rangeStart.AddHours(205).ToString("O"),null,"Expired"));
            Require(queue.GetArrayLength()==1);
            Require(queue.EnumerateArray().Any(x=>x.GetProperty("id").GetString()==filtered[0].GetProperty("id").GetString()));

            Console.WriteLine("H05_DB_PASS: profile,replay-literal-historical,no-duplication,same-slot,same-service,coverage-boundaries,read-only-projection,filter-before-limit,concurrent-book,cancel-once,occupied-swap,review-cancel-reschedule-races,outbox-failure-rollback");
            return 0;
        }
        catch(Npgsql.PostgresException e) { Console.Error.WriteLine("H05_DB_FAILED:"+stage+" SQLSTATE="+e.SqlState);return 1; }
        catch { Console.Error.WriteLine("H05_DB_FAILED:"+stage);return 1; }
    }
}
