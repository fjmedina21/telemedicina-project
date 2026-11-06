using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Common.Models.ApiResponses;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;

namespace Telemedicina.Api.Features.Patients;

[ApiController, Authorize, Route("api/v1")]
public sealed class PatientsController(Database database) : ApiControllerBase(database)
{
    [Authorize(Roles = "Patient"), HttpGet("patients/me")]
    [ProducesResponseType(typeof(ApiResponse<ProfileSnapshot?>), 200)]
    [ProducesResponseType(typeof(ErrorApiResponse), 401)]
    [ProducesResponseType(typeof(ErrorApiResponse), 403)]
    public IActionResult Profile()
    {
        using var c = Database.Open();
        var rows = Rows(c, null, "SELECT * FROM profiles WHERE user_id=@u", ("u", UserId(User)));
        if (rows.Count == 0) return Data(null);
        var row = rows[0];
        var body = JsonSerializer.Deserialize<JsonElement>((string)row["body"]!);
        var metadata = ProfileRules.Categories.ToDictionary(category => category,
            category => new DeclarationMetadata("SelfDeclared", (DateTime?)row[category + "_updated_at"]));
        return Data(new ProfileSnapshot(body, (int)row["version"]!, (DateTime)row["updated_at"]!,
            "SelfDeclared", metadata, ProfileRules.Completeness(body)));
    }

    [Authorize(Roles = "Patient"), HttpGet("patients/me/completeness")]
    [ProducesResponseType(typeof(ApiResponse<ProfileCompleteness>), 200)]
    [ProducesResponseType(typeof(ErrorApiResponse), 401)]
    [ProducesResponseType(typeof(ErrorApiResponse), 403)]
    public IActionResult Completeness()
    {
        using var c = Database.Open();
        var body = Scalar(c, null, "SELECT body FROM profiles WHERE user_id=@u", ("u", UserId(User))) as string;
        return Data(ProfileRules.Completeness(body is null ? default : JsonSerializer.Deserialize<JsonElement>(body)));
    }

    /// <summary>Reemplaza el perfil propio completo; requiere todos los campos y la versión exterior vigente.</summary>
    [Authorize(Roles = "Patient"), HttpPatch("patients/me")]
    [ProducesResponseType(typeof(ApiResponse<ProfileSaved>), 200)]
    [ProducesResponseType(typeof(ErrorApiResponse), 400)]
    [ProducesResponseType(typeof(ErrorApiResponse), 401)]
    [ProducesResponseType(typeof(ErrorApiResponse), 403)]
    [ProducesResponseType(typeof(ErrorApiResponse), 409)]
    public IActionResult SaveProfile(ProfileRequest request)
    {
        request = ProfileRules.Normalize(request);
        var errors = ProfileRules.Validate(request);
        if (errors.Length > 0)
            return StatusCode(400, new ErrorApiResponse(400, "Revise los datos del perfil.", Request.Path.Value ?? "/", errors)
            { Code = "INVALID_PROFILE", TraceId = HttpContext.TraceIdentifier });
        using var c = Database.Open(); using var t = Begin(c);
        Execute(c, t, "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", ("key", "profile:" + UserId(User)));
        var rows = Rows(c, t, "SELECT * FROM profiles WHERE user_id=@u FOR UPDATE", ("u", UserId(User)));
        var previous = rows.SingleOrDefault();
        if ((previous is null && request.Version != 0) || (previous is not null && (int)previous["version"]! != request.Version))
            return Fail(409, "VERSION_CONFLICT", "El perfil cambió. Actualice antes de guardar.");
        var oldBody = previous is null ? default : JsonSerializer.Deserialize<JsonElement>((string)previous["body"]!);
        var now = (DateTime)Scalar(c, t, "SELECT clock_timestamp()")!;
        DateTime? Stamp(string category, string[] values) => previous is null
            ? (values.Length == 0 ? null : now)
            : ProfileRules.SameCategory(oldBody, category, values) ? (DateTime?)previous[category + "_updated_at"] : now;
        var body = JsonSerializer.Serialize(request with { AdditionalProperties = null }, Json);
        if (previous is null)
            Execute(c, t, """
                INSERT INTO profiles(user_id,body,version,updated_at,allergies_updated_at,medications_updated_at,conditions_updated_at)
                VALUES(@u,@body,1,@now,@a::timestamptz,@m::timestamptz,@d::timestamptz)
                """, ("u", UserId(User)), ("body", body), ("now", now),
                ("a", Stamp("allergies", request.Allergies)), ("m", Stamp("medications", request.Medications)), ("d", Stamp("conditions", request.Conditions)));
        else
            Execute(c, t, """
                UPDATE profiles SET body=@body,version=version+1,updated_at=@now,
                    allergies_updated_at=@a::timestamptz,medications_updated_at=@m::timestamptz,conditions_updated_at=@d::timestamptz
                WHERE user_id=@u
                """, ("u", UserId(User)), ("body", body), ("now", now),
                ("a", Stamp("allergies", request.Allergies)), ("m", Stamp("medications", request.Medications)), ("d", Stamp("conditions", request.Conditions)));
        Log(c, t, "profile.updated", UserId(User)); t.Commit();
        return Data(new ProfileSaved(request.Version + 1));
    }
}
