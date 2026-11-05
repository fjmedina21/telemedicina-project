using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Telemedicina.Api.Features.Patients;
using static Telemedicina.Api.Infrastructure.Authentication.Security;

namespace Telemedicina.Api.Infrastructure.Persistence.Seeding;

// No generated ToString() containing credentials; hashes stay internal to the writer.
public sealed class DemoUser(string id, string email, string role, string passwordHash)
{
    public string Id { get; } = id;
    public string Email { get; } = email;
    public string Role { get; } = role;
    internal string PasswordHash { get; } = passwordHash;
}
public sealed record DemoPatient(string UserId, ProfileRequest Profile);
public sealed record DemoService(string Id, string PractitionerId, string Name, string Specialty,
    string PractitionerName, int Duration);
public sealed record DemoSlot(string Id, string ServiceId, string PractitionerId, DateTime StartUtc,
    DateTime EndUtc, bool Blocked);
public sealed record DemoAppointment(string Id, string UserId, DemoSlot Slot, string Status,
    string Coverage, string Insurer, string MemberLast4, string? ReviewerId)
{
    public bool HasTransition => Status != "PendingValidation" || Coverage != "Pending";
}

public sealed class DemoDataPlan
{
    public const string Version = "SEED-REALISTA-v1";
    public DateOnly BaseDate { get; }
    public IReadOnlyList<DemoUser> Users { get; }
    public IReadOnlyList<DemoPatient> Patients { get; }
    public IReadOnlyList<DemoService> Services { get; }
    public IReadOnlyList<DemoSlot> Slots { get; }
    public IReadOnlyList<DemoAppointment> Appointments { get; }

    private DemoDataPlan(DateOnly date, List<DemoUser> users, List<DemoPatient> patients,
        List<DemoService> services, List<DemoSlot> slots, List<DemoAppointment> appointments)
    {
        BaseDate = date; Users = users; Patients = patients; Services = services; Slots = slots;
        Appointments = appointments;
    }

    public static string Id(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(Version + ":" + key))[..16];
        // Deterministic UUID-shaped business identifiers; not used for tokens or secrets.
        bytes[7] = (byte)((bytes[7] & 15) | 80); bytes[8] = (byte)((bytes[8] & 63) | 128);
        return new Guid(bytes).ToString();
    }
    public static DateOnly Today() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo")).DateTime);
    public static DateTime Utc(DateOnly date, int hour, int minute = 0) =>
        new DateTimeOffset(date.Year, date.Month, date.Day, hour, minute, 0, TimeSpan.FromHours(-4)).UtcDateTime;

    public static DemoDataPlan Create(DateOnly date, IConfiguration configuration)
    {
        if (date.Year is < 2020 or > 9998) throw new DemoResetException("DEMO_INVALID_BASE_DATE");
        var primaryAdmin = Email(configuration["Milestone:AdministratorEmail"]);
        var primaryAdmission = Email(configuration["Milestone:AdmissionEmail"]);
        var adminPassword = Secret(configuration["Milestone:AdministratorPassword"]);
        var admissionPassword = Secret(configuration["Milestone:AdmissionPassword"]);
        var demoPassword = Secret(configuration["Milestone:DemoPassword"]);

        var firstNames = new[] { "Ana María", "José Luis", "María Isabel", "Juan Carlos", "Laura Patricia",
            "Carlos Alberto", "Rosa Elena", "Miguel Ángel", "Carmen Lucía", "Pedro Antonio", "Diana Carolina",
            "Luis Eduardo", "Teresa Milagros", "Andrés Manuel", "Paola Cristina", "Rafael Enrique",
            "Sofía Mercedes", "Daniel Alejandro", "Julia Beatriz", "Francisco Javier", "Elena Victoria",
            "Gabriel Esteban", "Clara Josefina", "Manuel Arturo" };
        var surnames = new[] { "Mejía Santos", "Pérez Núñez", "García Díaz", "Rodríguez Peña", "Reyes Gómez",
            "Castillo Ortiz", "Vargas Jiménez", "Fernández Cruz", "Ramírez Abreu", "Martínez Rosario",
            "Sánchez Guzmán", "Duarte Medina", "Méndez Acosta", "Torres Valdez", "Morales Paredes",
            "Espinal Cabrera", "Báez Tavárez", "Herrera Soriano", "Vásquez Paulino", "De la Rosa Gil",
            "Núñez Cordero", "Peña Santana", "Cruz Valera", "Ortiz Báez" };
        var cities = new[] { "Santo Domingo", "Santiago", "La Vega", "San Cristóbal" };
        var streets = new[] { "Calle del Parque", "Calle Las Palmas", "Calle Los Almendros", "Calle Primavera" };

        // Validate every email and credential before computing hashes or opening a connection.
        var emails = new List<string> { primaryAdmin, primaryAdmission, "admision.auxiliar.demo@example.invalid",
            "profesional.uno.demo@example.invalid", "profesional.dos.demo@example.invalid", "soporte.demo@example.invalid" };
        emails.AddRange(Enumerable.Range(1, 24).Select(i => $"paciente.{i:00}.demo@example.invalid"));
        if (emails.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 30)
            throw new DemoResetException("DEMO_EMAIL_COLLISION");

        var users = new List<DemoUser>
        {
            new(Id("staff:administrator"), primaryAdmin, "Administrator", Password(adminPassword)),
            new(Id("staff:admission:1"), primaryAdmission, "Admission", Password(admissionPassword)),
            new(Id("staff:admission:2"), emails[2], "Admission", Password(demoPassword)),
            new(Id("staff:practitioner:1"), emails[3], "Practitioner", Password(demoPassword)),
            new(Id("staff:practitioner:2"), emails[4], "Practitioner", Password(demoPassword)),
            new(Id("staff:support"), emails[5], "Support", Password(demoPassword))
        };
        var patients = new List<DemoPatient>();
        for (var i = 0; i < 24; i++)
        {
            var user = new DemoUser(Id($"patient:{i + 1:00}"), emails[i + 6], "Patient", Password(demoPassword));
            users.Add(user);
            patients.Add(new DemoPatient(user.Id, new ProfileRequest(
                firstNames[i], surnames[i] + " (DEMO)", new DateOnly(1965 + i, 1 + i % 12, 1 + i % 26),
                $"+180955501{i + 1:00}", $"DEMO · {streets[i % 4]} No. {10 + i}, Sector de demostración, {cities[i % 4]}",
                $"Contacto ficticio {i + 1:00} (DEMO)", $"+180955501{51 + i:00}",
                [], [], [], 1)));
        }
        var names = new[] { "Medicina general", "Medicina familiar", "Seguimiento de medicina interna",
            "Orientación psicológica", "Orientación nutricional", "Orientación dermatológica" };
        var practitionerNames = new[] { "Natalia del Carmen Reyes", "Samuel Antonio Méndez", "Verónica Isabel Cruz",
            "Diego Rafael Cabrera", "Lucía Mercedes Ortiz", "Emilio José Vargas" };
        var durations = new[] { 30, 30, 30, 40, 20, 20 };
        var services = Enumerable.Range(0, 6).Select(i => new DemoService(
            Id($"service:{i}"), Id($"catalog-practitioner:{i}"), names[i] + " · DEMO", names[i],
            practitionerNames[i] + " (DEMO)", durations[i])).ToList();

        var days = new List<DateOnly>(); var cursor = date;
        while (days.Count < 10)
        {
            cursor = cursor.AddDays(1);
            if (cursor.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days.Add(cursor);
        }
        var slots = new List<DemoSlot>();
        foreach (var day in days)
            foreach (var service in services)
                foreach (var startHour in new[] { 9, 14 })
                    for (var minute = 0; minute + service.Duration <= 180; minute += service.Duration)
                    {
                        var start = Utc(day, startHour).AddMinutes(minute);
                        slots.Add(new DemoSlot(Id($"slot:{day:yyyy-MM-dd}:{service.Id}:{startHour}:{minute}"),
                            service.Id, service.PractitionerId, start, start.AddMinutes(service.Duration),
                            startHour == 9 && minute == 0));
                    }
        // Spread the 30 examples across six services and five days; avoid blocked slots.
        var chosen = new List<DemoSlot>();
        for (var dayIndex = 0; dayIndex < 5; dayIndex++)
            foreach (var service in services)
                chosen.Add(slots.First(s => s.ServiceId == service.Id &&
                    DateOnly.FromDateTime(s.StartUtc.AddHours(-4)) == days[dayIndex] && !s.Blocked));
        var appointments = chosen.Select((slot, i) =>
        {
            var status = i < 12 ? "PendingValidation" : i < 20 ? "Confirmed" :
                i < 26 ? "Cancelled" : "PendingValidation";
            var coverage = i is >= 12 and < 20 ? "Approved" : i >= 26 ? "Denied" : "Pending";
            return new DemoAppointment(Id($"appointment:{i + 1:00}"), patients[i % 24].UserId, slot, status,
                coverage, new[] { "Aseguradora Demo Norte", "Aseguradora Demo Sur", "Aseguradora Demo Centro" }[i % 3],
                (1000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture),
                coverage is "Approved" or "Denied" ? users[1 + i % 2].Id : null);
        }).ToList();
        if (slots.Count != 800 || slots.Count(s => s.Blocked) != 60 ||
            appointments.Any(a => a.Slot.Blocked) || appointments.Select(a => a.Slot.Id).Distinct().Count() != 30)
            throw new DemoResetException("DEMO_PLAN_INVALID");
        return new DemoDataPlan(date, users, patients, services, slots, appointments);
    }

    private static string Email(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 254 ||
            !new EmailAddressAttribute().IsValid(value.Trim()))
            throw new DemoResetException("DEMO_INVALID_CREDENTIALS");
        return value.Trim().ToLowerInvariant();
    }
    private static string Secret(string? value)
    {
        if (value is null || value.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(value))
            throw new DemoResetException("DEMO_INVALID_CREDENTIALS");
        return value;
    }
}
