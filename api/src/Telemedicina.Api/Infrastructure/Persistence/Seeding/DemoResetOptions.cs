using System.Globalization;

namespace Telemedicina.Api.Infrastructure.Persistence.Seeding;

public sealed class DemoResetException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed record DemoResetOptions(bool Execute, DateOnly? BaseDate)
{
    public static DemoResetOptions Parse(string[] args)
    {
        var command = false; var execute = false; DateOnly? date = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--reset-demo-data" when !command: command = true; break;
                case "--execute" when !execute: execute = true; break;
                case "--base-date" when date is null && i + 1 < args.Length:
                    if (!DateOnly.TryParseExact(args[++i], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var parsed))
                        throw new DemoResetException("DEMO_INVALID_BASE_DATE");
                    date = parsed; break;
                default: throw new DemoResetException("DEMO_INVALID_ARGUMENTS");
            }
        }
        if (!command) throw new DemoResetException("DEMO_INVALID_ARGUMENTS");
        return new DemoResetOptions(execute, date);
    }
}
