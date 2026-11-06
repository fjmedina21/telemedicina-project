using Microsoft.AspNetCore.Builder;
using Npgsql;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Infrastructure.Persistence.Seeding;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Tests;

// Opt-in integration checks. Refuse ordinary development databases; never run in the default suite.
public static class DemoResetDbChecks
{
    public static int Run()
    {
        try
        {
            var connection = Environment.GetEnvironmentVariable("DEMO_RESET_TEST_CONNECTION_STRING");
            if (Environment.GetEnvironmentVariable("DEMO_RESET_DB_TESTS") != "1" || string.IsNullOrWhiteSpace(connection) ||
                !new NpgsqlConnectionStringBuilder(connection!).Database!.StartsWith("telemedicina_demo_reset_test", StringComparison.Ordinal))
                throw new InvalidOperationException("DEMO_TEST_DATABASE_REQUIRED");
            var config = DemoResetUnitChecks.Configuration(new() { ["ConnectionStrings:Telemedicina"] = connection });
            var environment = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" }).Environment;
            var database = new Database(config);
            var date = DemoDataPlan.Today();
            var before = Snapshot(database, false);
            var preview = DemoDataReset.Run(database, config, environment, new(false, date));
            Require(!preview.Committed && preview.After is null && before == Snapshot(database, false), "DRYRUN_CHANGED_DATA");
            Require(!string.IsNullOrWhiteSpace(preview.DatabaseName) && !string.IsNullOrWhiteSpace(preview.EndpointHost) &&
                preview.EndpointPort > 0, "DRYRUN_DESTINATION_MISSING");
            try
            {
                DemoDataReset.RunCore(database, config, environment, new(true, date),
                    () => throw new ProbeException());
                throw new InvalidOperationException("ROLLBACK_PROBE_NOT_REACHED");
            }
            catch (ProbeException) { }
            Require(before == Snapshot(database, false), "ROLLBACK_CHANGED_DATA_OR_TRIGGERS");
            var result = DemoDataReset.Run(database, config, environment, new(true, date));
            Require(result.Committed && result.After is not null &&
                result.Planned.All(pair => result.After[pair.Key] == pair.Value) &&
                result.After["appointments"] == 0 && result.After["appointment_history"] == 0 &&
                result.After["notifications"] == 0, "SEED_COUNT_MISMATCH");
            var first = Snapshot(database, true);
            DemoDataReset.Run(database, config, environment, new(true, date));
            Require(first == Snapshot(database, true), "BUSINESS_DATA_NOT_REPRODUCIBLE");
            AssertProtected(database, "audit");
            AssertProtected(database, "appointment_history");
            Console.WriteLine("DEMO_RESET_DB_CHECKS_PASS: dryrun,rollback,triggers,integrity,repetition");
            return 0;
        }
        catch { Console.Error.WriteLine("DEMO_RESET_DB_CHECKS_FAILED"); return 1; }
    }

    private static string Snapshot(Database database, bool functional)
    {
        using var c = database.Open(); using var t = c.BeginTransaction();
        var chunks = new List<string>();
        foreach (var table in DemoDataReset.Tables.Append("schema_versions").Order(StringComparer.Ordinal))
        {
            var projection = functional ?
                "to_jsonb(r)-ARRAY['password_hash','created_at','updated_at','occurred_at','reviewed_at']" : "to_jsonb(r)";
            chunks.Add((string)Scalar(c, t, "SELECT md5(coalesce(string_agg((" + projection +
                ")::text,E'\\n' ORDER BY (" + projection + ")::text),'')) FROM telemedicina_m1.\"" + table + "\" r")!);
        }
        chunks.Add((string)Scalar(c, t, """
            SELECT md5(string_agg(g.tgname||':'||g.tgenabled::text,',' ORDER BY g.tgname))
            FROM pg_trigger g JOIN pg_class r ON r.oid=g.tgrelid JOIN pg_namespace n ON n.oid=r.relnamespace
            WHERE n.nspname='telemedicina_m1' AND NOT g.tgisinternal
            """)!);
        // Hash object metadata outside the replacement scope without creating/changing public objects.
        chunks.Add((string)Scalar(c, t, """
            SELECT md5(coalesce(string_agg(r.oid::text||':'||r.relname||':'||r.relkind::text,',' ORDER BY r.oid),''))
            FROM pg_class r JOIN pg_namespace n ON n.oid=r.relnamespace WHERE n.nspname='public'
            """)!);
        return string.Join(":", chunks);
    }

    private static void AssertProtected(Database database, string table)
    {
        using var c = database.Open(); using var t = c.BeginTransaction();
        var rejected = false;
        try { Execute(c, t, "DELETE FROM telemedicina_m1.\"" + table + "\" WHERE false"); }
        catch (PostgresException error) when (error.SqlState == "P0001") { rejected = true; }
        t.Rollback();
        Require(rejected, "APPEND_ONLY_PROTECTION_MISSING");
    }
    private static void Require(bool condition, string code) { if (!condition) throw new InvalidOperationException(code); }
    private sealed class ProbeException : Exception { }
}
