using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;

namespace Telemedicina.Api.Infrastructure.Persistence.Seeding;

public static class SyntheticDataSeeder
{
    public static void Seed(Database database, IConfiguration config)
    {
        using var c = database.Open(); using var t = c.BeginTransaction();
        Execute(c, t, "SELECT pg_advisory_xact_lock(19041302)");
        const string service = "10000000-0000-4000-8000-000000000001";
        const string practitioner = "20000000-0000-4000-8000-000000000001";
        Execute(c, t, """
            INSERT INTO services(id,specialty,name,duration,is_synthetic)
            VALUES(@id,'Demostración','Servicio sintético: no habilitado para atención clínica',30,true)
            ON CONFLICT DO NOTHING
            """, ("id", service));
        Execute(c, t, "INSERT INTO practitioners(id,name) VALUES(@id,'Profesional de demostración') ON CONFLICT DO NOTHING",
            ("id", practitioner));
        Execute(c, t, "INSERT INTO practitioner_services(practitioner_id,service_id) VALUES(@p,@s) ON CONFLICT DO NOTHING",
            ("p", practitioner), ("s", service));
        for (var day = 1; day <= 14; day++)
            for (var hour = 9; hour <= 16; hour++)
            {
                var date = DateTime.UtcNow.Date.AddDays(day);
                var start = new DateTimeOffset(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.FromHours(-4)).UtcDateTime;
                var id = new Guid(System.Security.Cryptography.MD5.HashData(
                    System.Text.Encoding.UTF8.GetBytes(practitioner + start.ToString("O")))).ToString();
                Execute(c, t, """
                INSERT INTO slots(id,service_id,practitioner_id,start_utc,end_utc)
                VALUES(@id,@s,@p,@start,@end) ON CONFLICT DO NOTHING
                """, ("id", id), ("s", service), ("p", practitioner), ("start", start), ("end", start.AddMinutes(30)));
            }
        var email = config["Milestone:AdmissionEmail"];
        var password = config["Milestone:AdmissionPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new InvalidOperationException("Configure synthetic admission email and password (12+ chars).");
        Execute(c, t, """
            INSERT INTO users(id,email,password_hash,role,verified)
            VALUES(@id,@email,@hash,'Admission',true) ON CONFLICT DO NOTHING
            """, ("id", Guid.NewGuid().ToString()), ("email", email.Trim().ToLowerInvariant()),
            ("hash", Password(password)));
        Record(c, t, "seed", "synthetic.seed_applied", service, "seed"); t.Commit();
    }
}
