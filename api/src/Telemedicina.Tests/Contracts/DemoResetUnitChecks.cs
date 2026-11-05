using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Telemedicina.Api.Infrastructure.Persistence.Seeding;

namespace Telemedicina.Tests;

public static class DemoResetUnitChecks
{
    internal static IConfiguration Configuration(Dictionary<string, string?>? overrides = null)
    {
        var secret = Guid.NewGuid().ToString("N");
        var values = new Dictionary<string, string?>
        {
            ["Milestone:Enabled"] = "true", ["Mail:Mode"] = "development",
            ["Milestone:AdministratorEmail"] = "primary.admin.demo@example.invalid",
            ["Milestone:AdministratorPassword"] = secret,
            ["Milestone:AdmissionEmail"] = "primary.admission.demo@example.invalid",
            ["Milestone:AdmissionPassword"] = secret,
            ["Milestone:DemoPassword"] = secret
        };
        if (overrides is not null) foreach (var item in overrides) values[item.Key] = item.Value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    public static void Register(Action<string, Action> test, Action<bool, string> assert)
    {
        test("DemoReset_DefaultIsDryRun_ExecuteIsExplicit", () =>
        {
            var options = DemoResetOptions.Parse(["--reset-demo-data"]);
            assert(!options.Execute && options.BaseDate is null, "Default must not replace data.");
            var execute = DemoResetOptions.Parse(["--reset-demo-data", "--execute", "--base-date", "2040-01-02"]);
            assert(execute.Execute && execute.BaseDate == new DateOnly(2040, 1, 2), "Explicit date/execute must parse.");
            foreach (var invalid in new[]
            {
                new[] { "--execute" }, new[] { "--reset-demo-data", "--migrate" },
                new[] { "--reset-demo-data", "--seed-synthetic" },
                new[] { "--reset-demo-data", "--reset-demo-data" },
                new[] { "--reset-demo-data", "--execute", "--execute" },
                new[] { "--reset-demo-data", "--base-date", "invalid" }
            }) Reject(() => DemoResetOptions.Parse(invalid), assert);
        });
        test("DemoReset_EnvironmentAndMailGuard", () =>
        {
            foreach (var environment in new[] { "Production", "Staging" })
            {
                var host = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
                Reject(() => DemoDataReset.RequireEnvironment(Configuration(), host.Environment), assert);
            }
            var development = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            Reject(() => DemoDataReset.RequireEnvironment(Configuration(new() { ["Mail:Mode"] = "smtp" }), development.Environment), assert);
            Reject(() => DemoDataReset.RequireEnvironment(Configuration(new() { ["Milestone:Enabled"] = "false" }), development.Environment), assert);
        });
        test("DemoReset_CredentialsAndCollisionsRejectBeforeConnection", () =>
        {
            var date = new DateOnly(2040, 1, 2);
            Reject(() => DemoDataPlan.Create(date, Configuration(new() { ["Milestone:DemoPassword"] = null })), assert);
            Reject(() => DemoDataPlan.Create(date, Configuration(new() { ["Milestone:AdministratorEmail"] = "invalid" })), assert);
            Reject(() => DemoDataPlan.Create(date, Configuration(new()
                { ["Milestone:AdmissionEmail"] = "PRIMARY.ADMIN.DEMO@example.invalid" })), assert);
            Reject(() => DemoDataPlan.Create(date, Configuration(new()
                { ["Milestone:AdministratorEmail"] = "paciente.01.demo@example.invalid" })), assert);
        });
        test("DemoReset_DeterministicDatasetAndAgenda", () =>
        {
            var date = new DateOnly(2040, 1, 2); var config = Configuration();
            var plan = DemoDataPlan.Create(date, config);
            var repeat = DemoDataPlan.Create(date, config);
            assert(plan.Users.Count == 30 && plan.Patients.Count == 24 && plan.Services.Count == 6, "Approved entity totals.");
            assert(plan.Users.Select(user => user.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 30, "Unique accounts.");
            assert(plan.Users.Count(user => user.Role == "Administrator") == 1 &&
                plan.Users.Count(user => user.Role == "Admission") == 2 &&
                plan.Users.Count(user => user.Role == "Practitioner") == 2 &&
                plan.Users.Count(user => user.Role == "Support") == 1, "Approved staff distribution.");
            assert(plan.Slots.SequenceEqual(repeat.Slots) && plan.Appointments.SequenceEqual(repeat.Appointments), "Business data must repeat.");
            assert(plan.Slots.Count == 800 && plan.Slots.Count(slot => slot.Blocked) == 60, "Approved agenda totals.");
            assert(plan.Slots.Select(slot => DateOnly.FromDateTime(slot.StartUtc.AddHours(-4))).Distinct().Count() == 10, "Ten weekdays.");
            foreach (var slot in plan.Slots)
            {
                var local = slot.StartUtc.AddHours(-4); var end = slot.EndUtc.AddHours(-4);
                assert(local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
                    DateOnly.FromDateTime(local) > date, "Future weekdays relative to base date.");
                assert((local.TimeOfDay >= TimeSpan.FromHours(9) && end.TimeOfDay <= TimeSpan.FromHours(12)) ||
                    (local.TimeOfDay >= TimeSpan.FromHours(14) && end.TimeOfDay <= TimeSpan.FromHours(17)), "Working windows.");
                assert(slot.EndUtc - slot.StartUtc == TimeSpan.FromMinutes(plan.Services.Single(s => s.Id == slot.ServiceId).Duration),
                    "Slot duration must match its service.");
            }
            foreach (var group in plan.Slots.GroupBy(slot => slot.PractitionerId))
            {
                var ordered = group.OrderBy(slot => slot.StartUtc).ToArray();
                assert(ordered.Zip(ordered.Skip(1)).All(pair => pair.First.EndUtc <= pair.Second.StartUtc), "No practitioner overlaps.");
            }
            assert(plan.Appointments.Count == 30 && plan.Appointments.Count(a => a.HasTransition) == 18 &&
                plan.Appointments.All(a => !a.Slot.Blocked), "Thirty appointments, eighteen transitions, no blocked reservation.");
            assert(plan.Patients.All(patient => patient.Profile.LastName.Contains("(DEMO)") &&
                patient.Profile.BirthDate.AddYears(18) < date), "Synthetic adults with visible mark.");
            assert(DemoDataReset.Tables.Count == 17 && DemoDataReset.Expected()["appointment_history"] == 48 &&
                DemoDataReset.Expected()["email_outbox"] == 0, "Closed replacement manifest and expected counts.");
        });
    }

    private static void Reject(Action operation, Action<bool, string> assert)
    {
        var rejected = false;
        try { operation(); } catch (DemoResetException) { rejected = true; }
        assert(rejected, "Invalid operation must be rejected before data writes.");
    }
}
