using System.ComponentModel.DataAnnotations;
using Telemedicina.Api.Features.Identity;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;

namespace Telemedicina.Api.Infrastructure.Persistence.Seeding;

/// <summary>Explicit Development bootstrap; never changes an existing account's password/role.</summary>
public static class StaffSyntheticSeeder
{
    public static void Seed(Database database, IConfiguration config, IHostEnvironment environment)
    {
        StaffInvitations.RequireDevelopment(environment, config);
        using var c = database.Open(); using var t = c.BeginTransaction();
        Execute(c, t, "SELECT pg_advisory_xact_lock(19041303)");
        foreach (var role in new[] { "Administrator", "Practitioner", "Support" })
        {
            var email = config["Milestone:" + role + "Email"];
            var password = config["Milestone:" + role + "Password"];
            if (role != "Administrator" && string.IsNullOrEmpty(email) && string.IsNullOrEmpty(password)) continue;
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email.Trim()) ||
                email.Trim().Length > 254 || password is null || password.Length is < 12 or > 128)
                throw new InvalidOperationException("Configure private synthetic staff email/password (12-128 characters).");
            var normalized = email.Trim().ToLowerInvariant();
            Execute(c, t, """
                INSERT INTO users(id,email,password_hash,role,verified,active)
                VALUES(@id,@email,@hash,@role,true,true) ON CONFLICT DO NOTHING
                """, ("id", Guid.NewGuid().ToString()), ("email", normalized), ("hash", Password(password)), ("role", role));
            var found = Scalar(c, t, "SELECT role FROM users WHERE lower(email)=@email", ("email", normalized)) as string;
            if (found != role) throw new InvalidOperationException("Synthetic account collision; no role was changed.");
        }
        Record(c, t, "seed", "staff.synthetic_seed_applied", "staff", "seed");
        t.Commit();
    }
}
