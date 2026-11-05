using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Api.Shared.Contracts;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;
using Telemedicina.Api.Features.Appointments;
using Telemedicina.Api.Features.Notifications;

namespace Telemedicina.Api.Features.Patients;

[ApiController, Authorize, Route("api/v1")]
public sealed class PatientsController(Database database) : ApiControllerBase(database)
{
    [Authorize(Roles = "Patient"), HttpGet("patients/me")]
    public IActionResult Profile()
    {
        using var c = Database.Open();
        var rows = Rows(c, null, "SELECT body,version,updated_at FROM profiles WHERE user_id=@u", ("u", UserId(User)));
        return Data(rows.Count == 0 ? null : new
        {
            profile = JsonSerializer.Deserialize<JsonElement>((string)rows[0]["body"]!),
            version = rows[0]["version"],
            updatedAt = rows[0]["updated_at"],
            source = "SelfDeclared"
        });
    }
    [Authorize(Roles = "Patient"), HttpPatch("patients/me")]
    public IActionResult SaveProfile(ProfileRequest request)
    {
        if (request.BirthDate >= DateOnly.FromDateTime(DateTime.UtcNow) || request.BirthDate.Year < 1900 ||
            new[] { request.FirstName, request.LastName, request.Address, request.EmergencyName }.Any(string.IsNullOrWhiteSpace) ||
            request.Allergies is null || request.Medications is null || request.Conditions is null ||
            request.Allergies.Concat(request.Medications).Concat(request.Conditions).Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200))
            return Fail(400, "INVALID_PROFILE", "Revise los datos del perfil.");
        using var c = Database.Open(); using var t = Begin(c);
        Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", "profile:" + UserId(User)));
        var version = Scalar(c, t, "SELECT version FROM profiles WHERE user_id=@u FOR UPDATE", ("u", UserId(User)));
        if ((version is null && request.Version != 0) || (version is not null && (int)version != request.Version))
            return Fail(409, "VERSION_CONFLICT", "El perfil cambió. Actualice antes de guardar.");
        Execute(c, t, """
            INSERT INTO profiles(user_id,body,version) VALUES(@u,@body,1)
            ON CONFLICT(user_id) DO UPDATE SET body=excluded.body,version=profiles.version+1,updated_at=now()
            """, ("u", UserId(User)), ("body", JsonSerializer.Serialize(request, Json)));
        Log(c, t, "profile.updated", UserId(User)); t.Commit();
        return Data(new { version = request.Version + 1 });
    }


}
