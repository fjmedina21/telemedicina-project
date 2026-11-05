using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Infrastructure.Auditing;

public static class AuditLog
{
    public static void Record(Npgsql.NpgsqlConnection c, Npgsql.NpgsqlTransaction t,
        string actor, string action, string resource, string trace, string result = "Success") =>
        Execute(c, t, "INSERT INTO audit(id,actor_id,action,resource_id,trace_id,result) VALUES(@id,@a,@b,@r,@t,@result)",
            ("id", Guid.NewGuid().ToString()), ("a", actor), ("b", action), ("r", resource), ("t", trace), ("result", result));

}
