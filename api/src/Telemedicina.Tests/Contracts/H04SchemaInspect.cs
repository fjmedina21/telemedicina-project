using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Telemedicina.Api.Infrastructure.Persistence;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
namespace Telemedicina.Tests;
public static class H04SchemaInspect
{
    public static int Run(bool checkMigration = false)
    {
        try
        {
            if (Environment.GetEnvironmentVariable("MILESTONE_TEST_SYNTHETIC") != "1" ||
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Development" ||
                Environment.GetEnvironmentVariable("Mail__Mode") != "development") throw new InvalidOperationException();
            using var c = new Database(new ConfigurationBuilder().AddEnvironmentVariables().Build()).Open();
            using var t = c.BeginTransaction();
            long before = 0;
            if (checkMigration)
            {
                Execute(c,t,"SELECT pg_advisory_xact_lock(19041301)");
                before=Convert.ToInt64(Scalar(c,t,"SELECT count(*) FROM appointment_history"));
                var sql=File.ReadAllText("api/src/Telemedicina.Api/Infrastructure/Persistence/Migrations/008_appointment_history_trigger.sql");
                Execute(c,t,sql); Execute(c,t,sql);
                if (Convert.ToInt64(Scalar(c,t,"SELECT count(*) FROM appointment_history"))!=before)
                    throw new InvalidOperationException();
            }
            else Execute(c,t,"SET TRANSACTION READ ONLY");
            var triggers=Rows(c,t,"""
                SELECT tr.tgname AS name,tr.tgenabled::text AS enabled,pg_get_triggerdef(tr.oid) AS definition
                FROM pg_trigger tr JOIN pg_class cl ON cl.oid=tr.tgrelid
                JOIN pg_namespace ns ON ns.oid=cl.relnamespace
                WHERE ns.nspname='telemedicina_m1' AND cl.relname='appointments' AND NOT tr.tgisinternal
                ORDER BY tr.tgname
                """);
            if (checkMigration && (triggers.Count!=1 || (string?)triggers[0]["name"]!="appointment_history_written" ||
                (string?)triggers[0]["enabled"]!="O")) throw new InvalidOperationException();
            var replicationRole=Scalar(c,t,"SHOW session_replication_role");
            t.Rollback();
            Console.WriteLine(JsonSerializer.Serialize(new { diagnostic=checkMigration ? "H04_HISTORY_MIGRATION_TWICE_PASS_NO_BACKFILL" : "H04_SCHEMA_INSPECT",replicationRole,triggers }));
            return 0;
        }
        catch { Console.Error.WriteLine("H04_SCHEMA_INSPECT_FAILED: details-suppressed");return 1; }
    }
}
