using System.Data;
using System.Text.Json;
using Npgsql;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Infrastructure.Persistence.Seeding;

public sealed record DemoResetReport(string Dataset, string BaseDate, bool Committed,
    IReadOnlyDictionary<string, long> Before, IReadOnlyDictionary<string, long> Planned,
    IReadOnlyDictionary<string, long>? After, int BlockedSlots);

public static class DemoDataReset
{
    // Closed manifest. Dynamic SQL below uses only these constants, never user input.
    public static IReadOnlyList<string> Tables { get; } = Array.AsReadOnly(new[]
    {
        "users", "profiles", "sessions", "challenges", "dev_mailbox", "services", "practitioners",
        "slots", "appointments", "appointment_history", "idempotency", "audit", "practitioner_services",
        "notification_preferences", "notifications", "email_outbox", "staff_invitations"
    });
    private const string Schema = "telemedicina_m1";
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
        using var c = database.Open();
        using var t = c.BeginTransaction(options.Execute ? IsolationLevel.ReadCommitted : IsolationLevel.RepeatableRead);
        if (options.Execute)
        {
            foreach (var key in new[] { 19041301, 19041302, 19041303, 19041304 })
                if (Scalar(c, t, "SELECT pg_try_advisory_xact_lock(@key)", ("key", key)) is not true)
                    throw new DemoResetException("DEMO_BUSY");
        }
        if (!options.Execute) Execute(c, t, "SET TRANSACTION READ ONLY");
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
                before, expected, null, 60);

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
            before, expected, after, 60);
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
        if (!versions.Select(row => (int)row["version"]!).SequenceEqual(new[] { 1, 2, 3, 4, 5 }))
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
            throw new DemoResetException("DEMO_TRIGGER_MISMATCH");
    }

    public static IReadOnlyDictionary<string, long> Expected() =>
        Tables.ToDictionary(table => table, table => table switch
        {
            "users" => 30L, "profiles" => 24, "services" or "practitioners" or "practitioner_services" => 6,
            "slots" => 800, "appointments" => 30, "appointment_history" or "notifications" => 48,
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
        foreach (var appointment in plan.Appointments)
        {
            Execute(c, t, "SELECT set_config('telemedicina.actor',@actor,true)", ("actor", appointment.UserId));
            Execute(c, t, """
                INSERT INTO telemedicina_m1.appointments(id,user_id,slot_id,practitioner_id,start_utc,end_utc,
                    status,coverage,insurer,member_last4)
                VALUES(@id,@u,@slot,@p,@start,@end,'PendingValidation','Pending',@insurer,@member)
                """, ("id", appointment.Id), ("u", appointment.UserId), ("slot", appointment.Slot.Id),
                ("p", appointment.Slot.PractitionerId), ("start", appointment.Slot.StartUtc),
                ("end", appointment.Slot.EndUtc), ("insurer", appointment.Insurer), ("member", appointment.MemberLast4));
            Notify(c, t, appointment, 1, "requested",
                "Tu solicitud fue registrada. Inicia sesión para consultar su estado.");
            if (appointment.HasTransition)
            {
                Execute(c, t, "SELECT set_config('telemedicina.actor',@actor,true)",
                    ("actor", appointment.ReviewerId ?? appointment.UserId));
                Execute(c, t, """
                    UPDATE telemedicina_m1.appointments SET status=@status,coverage=@coverage,version=2,
                        evidence=@evidence,coverage_valid_until=@valid,reviewed_by=@reviewer,
                        reviewed_at=CASE WHEN @reviewer::text IS NULL THEN NULL ELSE now() END WHERE id=@id
                    """, ("status", appointment.Status), ("coverage", appointment.Coverage),
                    ("evidence", appointment.ReviewerId is null ? null : "DEMO: resultado simulado; sin validación ARS"),
                    ("valid", appointment.Coverage == "Approved" ? appointment.Slot.EndUtc.AddDays(1) : (DateTime?)null),
                    ("reviewer", appointment.ReviewerId), ("id", appointment.Id));
                Notify(c, t, appointment, 2, appointment.Status == "Cancelled" ? "cancelled" : "updated",
                    appointment.Status == "Cancelled" ?
                        "Tu cita fue cancelada. Inicia sesión para consultar los detalles." :
                        "El estado de tu cita cambió. Inicia sesión para consultar los detalles.");
            }
        }
        Execute(c, t, """
            INSERT INTO telemedicina_m1.audit(id,actor_id,action,resource_id,trace_id,result)
            VALUES(@id,@actor,'demo.reset_seed_applied',@resource,'demo-reset','Success')
            """, ("id", DemoDataPlan.Id("audit:reset")), ("actor", admin.Id),
            ("resource", DemoDataPlan.Version + ";users=30;profiles=24;services=6;practitioners=6;slots=800;blocked=60;appointments=30;history=48"));
    }

    private static void Notify(NpgsqlConnection c, NpgsqlTransaction t, DemoAppointment appointment,
        int version, string kind, string text)
    {
        var key = "demo:" + appointment.Id + ":" + version + ":" + kind;
        Execute(c, t, """
            INSERT INTO telemedicina_m1.notifications(id,user_id,event_key,kind,body,resource_id,resource_version)
            VALUES(@id,@u,@key,@kind,@body,@resource,@version)
            """, ("id", DemoDataPlan.Id("notification:" + key)), ("u", appointment.UserId), ("key", key),
            ("kind", kind), ("body", text), ("resource", appointment.Id), ("version", version));
    }

    private static void AssertIntegrity(NpgsqlConnection c, NpgsqlTransaction t, IReadOnlyDictionary<string, long> expected)
    {
        var actual = Counts(c, t);
        if (expected.Any(pair => actual[pair.Key] != pair.Value))
            throw new DemoResetException("DEMO_COUNT_MISMATCH");
        if (Convert.ToInt64(Scalar(c, t, """
            SELECT count(*) FROM telemedicina_m1.slots WHERE blocked
            """)) != 60 || Convert.ToInt64(Scalar(c, t, """
            SELECT count(*) FROM telemedicina_m1.appointments a JOIN telemedicina_m1.slots s ON s.id=a.slot_id
            WHERE s.blocked OR s.start_utc<>a.start_utc OR s.end_utc<>a.end_utc
            """)) != 0 || Convert.ToInt64(Scalar(c, t, """
            SELECT count(*) FROM telemedicina_m1.slots a JOIN telemedicina_m1.slots b
            ON a.practitioner_id=b.practitioner_id AND a.id<b.id
            AND tstzrange(a.start_utc,a.end_utc,'[)') && tstzrange(b.start_utc,b.end_utc,'[)')
            """)) != 0)
            throw new DemoResetException("DEMO_INTEGRITY_FAILED");
        var distribution = Rows(c, t, """
            SELECT status,coverage,count(*) AS total FROM telemedicina_m1.appointments
            GROUP BY status,coverage
            """).ToDictionary(row => (string)row["status"]! + "/" + row["coverage"],
                row => (long)row["total"]!);
        if (distribution.Count != 4 || distribution.GetValueOrDefault("PendingValidation/Pending") != 12 ||
            distribution.GetValueOrDefault("Confirmed/Approved") != 8 ||
            distribution.GetValueOrDefault("Cancelled/Pending") != 6 ||
            distribution.GetValueOrDefault("PendingValidation/Denied") != 4)
            throw new DemoResetException("DEMO_DISTRIBUTION_FAILED");
        // Check restored trigger identities as well as data before committing.
        Preflight(c, t);
    }
}
