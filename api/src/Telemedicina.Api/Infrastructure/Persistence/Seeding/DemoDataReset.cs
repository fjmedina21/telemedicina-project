using System.Data;
using System.Net;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Infrastructure.Persistence.Seeding;

public sealed record DemoResetReport(string Dataset, string BaseDate, bool Committed,
    IReadOnlyDictionary<string, long> Before, IReadOnlyDictionary<string, long> Planned,
    IReadOnlyDictionary<string, long>? After, int BlockedSlots, string DatabaseName,
    string EndpointHost, int EndpointPort);

public static class DemoDataReset
{
    // Closed manifest. Dynamic SQL below uses only these constants, never user input.
    public static IReadOnlyList<string> Tables { get; } = Array.AsReadOnly(new[]
    {
        "users", "profiles", "sessions", "challenges", "dev_mailbox", "services", "practitioners",
        "slots", "appointments", "appointment_history", "idempotency", "audit", "practitioner_services",
        "notification_preferences", "notifications", "email_outbox", "staff_invitations",
        "check_in_attempts"
    });
    private const string Schema = "telemedicina_m1";
    private const string AllowedRemoteHost = "pg-2db5ca83-telemedicina-iso910.l.aivencloud.com";
    private const int AllowedRemotePort = 10036;
    private const string AllowedRemoteDatabase = "defaultdb";
    private static string Qualified(string table) => "\"" + Schema + "\".\"" + table + "\"";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void RequireEnvironment(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Milestone:Enabled") ||
            configuration["Mail:Mode"] != "development")
            throw new DemoResetException("DEMO_DEVELOPMENT_ONLY");
    }

    public static DemoResetReport Run(Database database, IConfiguration configuration, IHostEnvironment environment,
        DemoResetOptions options) => RunCore(database, configuration, environment, options, null);

    // Deliberately internal: only the test assembly can inject a rollback probe, never CLI/HTTP/config.
    internal static DemoResetReport RunCore(Database database, IConfiguration configuration,
        IHostEnvironment environment, DemoResetOptions options, Action? afterTruncate)
    {
        RequireEnvironment(configuration, environment);
        var plan = DemoDataPlan.Create(options.BaseDate ?? DemoDataPlan.Today(), configuration);
        if (plan.Slots.Min(s => s.StartUtc) <= DateTime.UtcNow)
            throw new DemoResetException("DEMO_BASE_DATE_PAST");
        if (options.Execute) RequireAllowedTarget(configuration);
        using var c = database.Open();
        using var t = c.BeginTransaction(options.Execute ? IsolationLevel.ReadCommitted : IsolationLevel.RepeatableRead);
        if (options.Execute)
        {
            foreach (var key in new[] { 19041301, 19041302, 19041303, 19041304 })
                if (Scalar(c, t, "SELECT pg_try_advisory_xact_lock(@key)", ("key", key)) is not true)
                    throw new DemoResetException("DEMO_BUSY");
        }
        if (!options.Execute) Execute(c, t, "SET TRANSACTION READ ONLY");
        var endpoint = new NpgsqlConnectionStringBuilder(c.ConnectionString);
        Preflight(c, t);
        if (options.Execute)
        {
            var locked = Tables.Append("schema_versions").Order(StringComparer.Ordinal).Select(Qualified);
            Execute(c, t, "LOCK TABLE " + string.Join(",", locked) + " IN ACCESS EXCLUSIVE MODE NOWAIT");
            // Recheck manifest/versions/triggers after all target locks, before the first write.
            Preflight(c, t);
        }
        var before = Counts(c, t);
        var expected = Expected();
        if (!options.Execute)
            return new DemoResetReport(DemoDataPlan.Version, plan.BaseDate.ToString("yyyy-MM-dd"), false,
                before, expected, null, 60, endpoint.Database ?? "unknown", endpoint.Host ?? "unknown", endpoint.Port);

        Execute(c, t, "ALTER TABLE telemedicina_m1.audit DISABLE TRIGGER audit_no_mutation");
        Execute(c, t, "ALTER TABLE telemedicina_m1.appointment_history DISABLE TRIGGER history_no_mutation");
        Execute(c, t, "TRUNCATE TABLE " + string.Join(",", Tables.Select(Qualified)) + " RESTART IDENTITY");
        Execute(c, t, "ALTER TABLE telemedicina_m1.audit ENABLE TRIGGER audit_no_mutation");
        Execute(c, t, "ALTER TABLE telemedicina_m1.appointment_history ENABLE TRIGGER history_no_mutation");
        afterTruncate?.Invoke();
        // Existing trigger SQL is unqualified. Do not let it resolve to public.
        Execute(c, t, "SET LOCAL search_path TO telemedicina_m1, pg_catalog");
        Insert(c, t, plan);
        AssertIntegrity(c, t, expected);
        var after = Counts(c, t);
        t.Commit();
        return new DemoResetReport(DemoDataPlan.Version, plan.BaseDate.ToString("yyyy-MM-dd"), true,
            before, expected, after, 60, endpoint.Database ?? "unknown", endpoint.Host ?? "unknown", endpoint.Port);
    }

    internal static void RequireAllowedTarget(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Telemedicina");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new DemoResetException("DEMO_DATABASE_TARGET_NOT_ALLOWED");
        var endpoint = Database.ParseConnectionString(connectionString);
        var expectedDatabase = configuration["DemoReset:ExpectedDatabase"];
        var localHost = string.Equals(endpoint.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(endpoint.Host, out var address) && IPAddress.IsLoopback(address);

        if (localHost)
        {
            if (!string.IsNullOrWhiteSpace(endpoint.Database) &&
                !string.IsNullOrWhiteSpace(expectedDatabase) &&
                string.Equals(endpoint.Database, expectedDatabase.Trim(), StringComparison.Ordinal))
                return;
            throw new DemoResetException("DEMO_DATABASE_TARGET_NOT_ALLOWED");
        }

        // Configuration confirms the single hardcoded remote target; it cannot extend the allowlist.
        var expectedHost = configuration["DemoReset:ExpectedHost"];
        var expectedPort = configuration["DemoReset:ExpectedPort"];
        var expectedRemoteDatabase = configuration["DemoReset:ExpectedDatabase"];
        if (!string.Equals(endpoint.Host, AllowedRemoteHost, StringComparison.OrdinalIgnoreCase) ||
            endpoint.Port != AllowedRemotePort ||
            !string.Equals(endpoint.Database, AllowedRemoteDatabase, StringComparison.Ordinal) ||
            !string.Equals(expectedHost?.Trim(), AllowedRemoteHost, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(expectedPort, NumberStyles.None, CultureInfo.InvariantCulture, out var confirmedPort) ||
            confirmedPort != AllowedRemotePort ||
            !string.Equals(expectedRemoteDatabase?.Trim(), AllowedRemoteDatabase, StringComparison.Ordinal))
            throw new DemoResetException("DEMO_DATABASE_TARGET_NOT_ALLOWED");
    }

    private static void Preflight(NpgsqlConnection c, NpgsqlTransaction t)
    {
        var tables = Rows(c, t, """
            SELECT r.relname,r.relkind::text AS kind,
                (pg_has_role(current_user,r.relowner,'USAGE') OR
                 (SELECT rolsuper FROM pg_roles WHERE rolname=current_user)) AS can_own
            FROM pg_class r JOIN pg_namespace n ON n.oid=r.relnamespace
            WHERE n.nspname='telemedicina_m1' AND r.relkind IN ('r','p','f') ORDER BY r.relname
            """);
        if (!tables.Select(row => (string)row["relname"]!).SequenceEqual(
            Tables.Append("schema_versions").Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            tables.Any(row => (string)row["kind"]! != "r"))
            throw new DemoResetException("DEMO_SCHEMA_MANIFEST_MISMATCH");
        if (tables.Any(row => row["can_own"] is not true))
            throw new DemoResetException("DEMO_OWNER_REQUIRED");
        var versions = Rows(c, t, "SELECT version FROM telemedicina_m1.schema_versions ORDER BY version");
        if (!versions.Select(row => (int)row["version"]!).SequenceEqual(Enumerable.Range(1, 10)))
            throw new DemoResetException("DEMO_SCHEMA_VERSION_MISMATCH");
        // Reject references that would require CASCADE or truncate another schema/table.
        if (Convert.ToInt64(Scalar(c, t, """
            SELECT count(*) FROM pg_constraint k
            JOIN pg_class parent ON parent.oid=k.confrelid JOIN pg_namespace pn ON pn.oid=parent.relnamespace
            JOIN pg_class child ON child.oid=k.conrelid JOIN pg_namespace cn ON cn.oid=child.relnamespace
            WHERE k.contype='f' AND pn.nspname='telemedicina_m1' AND parent.relname=ANY(@tables::text[])
            AND (cn.nspname<>'telemedicina_m1' OR NOT(child.relname=ANY(@tables::text[])))
            """, ("tables", Tables.ToArray()))) != 0)
            throw new DemoResetException("DEMO_EXTERNAL_REFERENCE");
        var triggers = Rows(c, t, """
            SELECT r.relname,g.tgname,g.tgtype::integer AS type,g.tgenabled::text AS enabled,
                p.proname,nf.nspname AS function_schema,g.tgnargs::integer AS arguments,
                g.tgqual IS NULL AS unconditional
            FROM pg_trigger g JOIN pg_class r ON r.oid=g.tgrelid
            JOIN pg_namespace n ON n.oid=r.relnamespace JOIN pg_proc p ON p.oid=g.tgfoid
            JOIN pg_namespace nf ON nf.oid=p.pronamespace
            WHERE n.nspname='telemedicina_m1' AND NOT g.tgisinternal ORDER BY g.tgname
            """);
        var expected = new Dictionary<string, (string Table, string Function, int Type)>
        {
            ["audit_no_mutation"] = ("audit", "protect_audit", 58),
            ["history_no_mutation"] = ("appointment_history", "protect_audit", 58),
            ["appointment_history_written"] = ("appointments", "track_appointment", 21)
        };
        if (triggers.Count != expected.Count || triggers.Any(row =>
            !expected.TryGetValue((string)row["tgname"]!, out var spec) ||
            (string)row["relname"]! != spec.Table || (string)row["proname"]! != spec.Function ||
            (string)row["function_schema"]! != Schema || (int)row["type"]! != spec.Type ||
            (string)row["enabled"]! != "O" || (int)row["arguments"]! != 0 || row["unconditional"] is not true))
        {
            var actual = triggers.Select(row =>
                $"{SafeIdentifier(row["relname"])}.{SafeIdentifier(row["tgname"])}:" +
                $"{row["type"]}/{row["enabled"]}:{SafeIdentifier(row["function_schema"])}." +
                $"{SafeIdentifier(row["proname"])}:args={row["arguments"]}:unconditional={row["unconditional"]}");
            throw new DemoResetException($"DEMO_TRIGGER_MISMATCH count={triggers.Count} actual=[{string.Join(",", actual)}]");
        }
    }

    private static string SafeIdentifier(object? value) => value is string text && text.Length <= 63 &&
        text.All(character => char.IsAsciiLetterOrDigit(character) || character == '_') ? text : "invalid";

    public static IReadOnlyDictionary<string, long> Expected() =>
        Tables.ToDictionary(table => table, table => table switch
        {
            "users" => 30L, "profiles" => 24, "services" or "practitioners" or "practitioner_services" => 6,
            "slots" => 800,
            "appointments" or "appointment_history" or "notifications" or "check_in_attempts" => 0,
            "notification_preferences" => 24, "audit" => 1, _ => 0
        }, StringComparer.Ordinal);

    private static Dictionary<string, long> Counts(NpgsqlConnection c, NpgsqlTransaction t) =>
        Tables.ToDictionary(table => table,
            table => Convert.ToInt64(Scalar(c, t, "SELECT count(*) FROM " + Qualified(table))), StringComparer.Ordinal);

    private static void Insert(NpgsqlConnection c, NpgsqlTransaction t, DemoDataPlan plan)
    {
        var admin = plan.Users.Single(user => user.Role == "Administrator");
        foreach (var user in plan.Users)
            Execute(c, t, """
                INSERT INTO telemedicina_m1.users(id,email,password_hash,role,verified,active,created_by,failed_logins,locked_until)
                VALUES(@id,@email,@hash,@role,true,true,@creator,0,0)
                """, ("id", user.Id), ("email", user.Email), ("hash", user.PasswordHash), ("role", user.Role), ("creator", user.Id == admin.Id ? null : admin.Id));
        foreach (var patient in plan.Patients)
        {
            Execute(c, t, "INSERT INTO telemedicina_m1.profiles(user_id,body) VALUES(@id,@body)",
                ("id", patient.UserId), ("body", JsonSerializer.Serialize(patient.Profile, Json)));
            Execute(c, t, "INSERT INTO telemedicina_m1.notification_preferences(user_id,email_enabled) VALUES(@id,false)",
                ("id", patient.UserId));
        }
        foreach (var service in plan.Services)
        {
            Execute(c, t, """
                INSERT INTO telemedicina_m1.services(id,specialty,name,duration,active,is_synthetic,approval_reference)
                VALUES(@id,@specialty,@name,@duration,true,true,NULL)
                """, ("id", service.Id), ("specialty", service.Specialty), ("name", service.Name), ("duration", service.Duration));
            Execute(c, t, "INSERT INTO telemedicina_m1.practitioners(id,name,license_ref,active) VALUES(@id,@name,NULL,true)",
                ("id", service.PractitionerId), ("name", service.PractitionerName));
            Execute(c, t, "INSERT INTO telemedicina_m1.practitioner_services(practitioner_id,service_id) VALUES(@p,@s)",
                ("p", service.PractitionerId), ("s", service.Id));
        }
        foreach (var slot in plan.Slots)
            Execute(c, t, """
                INSERT INTO telemedicina_m1.slots(id,service_id,practitioner_id,start_utc,end_utc,blocked)
                VALUES(@id,@s,@p,@start,@end,@blocked)
                """, ("id", slot.Id), ("s", slot.ServiceId), ("p", slot.PractitionerId),
                ("start", slot.StartUtc), ("end", slot.EndUtc), ("blocked", slot.Blocked));
        Execute(c, t, """
            INSERT INTO telemedicina_m1.audit(id,actor_id,action,resource_id,trace_id,result)
            VALUES(@id,@actor,'demo.reset_seed_applied',@resource,'demo-reset','Success')
            """, ("id", DemoDataPlan.Id("audit:reset")), ("actor", admin.Id),
            ("resource", DemoDataPlan.Version + ";users=30;profiles=24;services=6;practitioners=6;slots=800;blocked=60;appointments=0;history=0;notifications=0"));
    }

    private static void AssertIntegrity(NpgsqlConnection c, NpgsqlTransaction t, IReadOnlyDictionary<string, long> expected)
    {
        var actual = Counts(c, t);
        if (expected.Any(pair => actual[pair.Key] != pair.Value))
            throw new DemoResetException("DEMO_COUNT_MISMATCH");
        if (Convert.ToInt64(Scalar(c, t, """
            SELECT count(*) FROM telemedicina_m1.slots WHERE blocked
            """)) != 60 || Convert.ToInt64(Scalar(c, t, """
            SELECT count(*) FROM telemedicina_m1.slots a JOIN telemedicina_m1.slots b
            ON a.practitioner_id=b.practitioner_id AND a.id<b.id
            AND tstzrange(a.start_utc,a.end_utc,'[)') && tstzrange(b.start_utc,b.end_utc,'[)')
            """)) != 0)
            throw new DemoResetException("DEMO_INTEGRITY_FAILED");
        if (actual["appointments"] != 0 || actual["appointment_history"] != 0 || actual["notifications"] != 0)
            throw new DemoResetException("DEMO_APPOINTMENT_DATA_NOT_EMPTY");
        // Check restored trigger identities as well as data before committing.
        Preflight(c, t);
    }
}
