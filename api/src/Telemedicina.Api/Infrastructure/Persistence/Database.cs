using Npgsql;
using System.Text;

namespace Telemedicina.Api.Infrastructure.Persistence;

public sealed class Database
{
    private readonly string _connectionString;
    public Database(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("Telemedicina")
            ?? throw new InvalidOperationException("Configure ConnectionStrings__Telemedicina outside source control.");
        if (configured.StartsWith("postgres://") || configured.StartsWith("postgresql://"))
        {
            var uri = new Uri(configured);
            var credentials = uri.UserInfo.Split(':', 2);
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                Username = Uri.UnescapeDataString(credentials[0]),
                Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : "",
                SslMode = SslMode.Require,
                IncludeErrorDetail = false,
                Timeout = 15
            };
            _connectionString = builder.ConnectionString;
        }
        else _connectionString = new NpgsqlConnectionStringBuilder(configured)
        {
            IncludeErrorDetail = false
        }.ConnectionString;
    }
    public NpgsqlConnection Open()
    {
        var c = new NpgsqlConnection(_connectionString);
        c.Open();
        Execute(c, null, "SET search_path TO telemedicina_m1, public;");
        return c;
    }
    public void Migrate(string path)
    {
        using var c = Open();
        using var t = c.BeginTransaction();
        Execute(c, t, "SELECT pg_advisory_xact_lock(19041301);");
        foreach (var migration in Directory.GetFiles(Path.GetDirectoryName(path)!, "*.sql").Order(StringComparer.Ordinal))
            Execute(c, t, File.ReadAllText(migration, Encoding.UTF8));
        t.Commit();
    }
    public static object? Scalar(NpgsqlConnection c, NpgsqlTransaction? t, string sql,
        params (string, object?)[] parameters)
    {
        using var cmd = Command(c, t, sql, parameters);
        return cmd.ExecuteScalar();
    }
    public static int Execute(NpgsqlConnection c, NpgsqlTransaction? t, string sql,
        params (string, object?)[] parameters)
    {
        using var cmd = Command(c, t, sql, parameters);
        return cmd.ExecuteNonQuery();
    }
    public static List<Dictionary<string, object?>> Rows(NpgsqlConnection c, NpgsqlTransaction? t,
        string sql, params (string, object?)[] parameters)
    {
        using var cmd = Command(c, t, sql, parameters);
        using var reader = cmd.ExecuteReader();
        var result = new List<Dictionary<string, object?>>();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            result.Add(row);
        }
        return result;
    }
    private static NpgsqlCommand Command(NpgsqlConnection c, NpgsqlTransaction? t, string sql,
        (string, object?)[] parameters)
    {
        var cmd = new NpgsqlCommand(sql, c, t);
        foreach (var (name, value) in parameters)
            cmd.Parameters.Add(new NpgsqlParameter(name, value ?? DBNull.Value));
        return cmd;
    }
}
