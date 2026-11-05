using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Api.Shared.Contracts;
using static Telemedicina.Api.Infrastructure.Persistence.Database;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using static Telemedicina.Api.Infrastructure.Auditing.AuditLog;
using Npgsql;

namespace Telemedicina.Api.Features.Catalog;

[ApiController, Authorize(Roles = "Admission"), Route("api/v1/admin/catalog")]
public sealed class CatalogController(Database database) : ApiControllerBase(database)
{
    [HttpGet("services")]
    public IActionResult Services()
    {
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT id,name,specialty,duration AS "durationMinutes",active,is_synthetic AS "isSynthetic",
            approval_reference AS "approvalReference",version FROM services ORDER BY name
            """));
    }
    [HttpPost("services")]
    public IActionResult CreateService(CatalogServiceRequest request) => SaveService(null, request);
    [HttpPatch("services/{id:guid}")]
    public IActionResult UpdateService(Guid id, CatalogServiceRequest request) => SaveService(id.ToString(), request);
    private IActionResult SaveService(string? id, CatalogServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Specialty) ||
            (request.Active && !request.IsSynthetic && string.IsNullOrWhiteSpace(request.ApprovalReference)))
            return Fail(400, "CATALOG_VALIDATION", "Complete nombre, especialidad y referencia de habilitación si el servicio no es sintético.");
        using var c = Database.Open(); using var t = Begin(c);
        var created = id is null; id ??= Guid.NewGuid().ToString(); var version = 1;
        if (!created)
        {
            var row = Rows(c, t, "SELECT version,duration FROM services WHERE id=@id FOR UPDATE", ("id", id)).FirstOrDefault();
            if (row is null) return NotFound();
            if ((int)row["version"]! != request.Version) return Fail(409, "VERSION_CONFLICT", "El servicio cambió. Actualice la lista.");
            if ((int)row["duration"]! != request.DurationMinutes &&
                Convert.ToInt64(Scalar(c, t, "SELECT count(*) FROM slots WHERE service_id=@id AND start_utc>now()", ("id", id))) > 0)
                return Fail(409, "EXISTING_SCHEDULE", "El servicio tiene horarios futuros con otra duración; cree una versión nueva del servicio.");
            version = request.Version + 1;
            Execute(c, t, """
                UPDATE services SET name=@name,specialty=@specialty,duration=@duration,active=@active,
                is_synthetic=@synthetic,approval_reference=@reference,version=@version WHERE id=@id
                """, ("name", request.Name.Trim()), ("specialty", request.Specialty.Trim()), ("duration", request.DurationMinutes),
                ("active", request.Active), ("synthetic", request.IsSynthetic), ("reference", request.ApprovalReference?.Trim()),
                ("version", version), ("id", id));
        }
        else Execute(c, t, """
            INSERT INTO services(id,name,specialty,duration,active,is_synthetic,approval_reference)
            VALUES(@id,@name,@specialty,@duration,@active,@synthetic,@reference)
            """, ("id", id), ("name", request.Name.Trim()), ("specialty", request.Specialty.Trim()), ("duration", request.DurationMinutes),
            ("active", request.Active), ("synthetic", request.IsSynthetic), ("reference", request.ApprovalReference?.Trim()));
        Record(c, t, UserId(User), created ? "catalog.service_created" : "catalog.service_updated", id, HttpContext.TraceIdentifier);
        t.Commit(); return Data(new { id, version }, created ? 201 : 200);
    }

    [HttpGet("practitioners")]
    public IActionResult Practitioners()
    {
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT p.id,p.name,p.active,p.version,p.license_ref AS "licenseRef",
                ARRAY(SELECT service_id FROM practitioner_services ps
                    WHERE ps.practitioner_id=p.id AND ps.active ORDER BY service_id) AS "serviceIds"
            FROM practitioners p ORDER BY p.name
            """));
    }
    [HttpPost("practitioners")]
    public IActionResult CreatePractitioner(CatalogPractitionerRequest request) => SavePractitioner(null, request);
    [HttpPatch("practitioners/{id:guid}")]
    public IActionResult UpdatePractitioner(Guid id, CatalogPractitionerRequest request) => SavePractitioner(id.ToString(), request);
    private IActionResult SavePractitioner(string? id, CatalogPractitionerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.ServiceIds is null || request.ServiceIds.Any(x => !Guid.TryParse(x, out _)))
            return Fail(400, "CATALOG_VALIDATION", "Revise nombre y servicios asignados.");
        var ids = request.ServiceIds.Select(x => Guid.Parse(x).ToString()).Distinct().ToArray();
        using var c = Database.Open(); using var t = Begin(c);
        if (Convert.ToInt64(Scalar(c, t, "SELECT count(*) FROM services WHERE id=ANY(@ids)", ("ids", ids))) != ids.Length)
            return Fail(400, "UNKNOWN_SERVICE", "Uno de los servicios no existe.");
        var created = id is null; id ??= Guid.NewGuid().ToString(); var version = 1;
        if (!created)
        {
            var old = Scalar(c, t, "SELECT version FROM practitioners WHERE id=@id FOR UPDATE", ("id", id));
            if (old is null) return NotFound();
            if ((int)old != request.Version) return Fail(409, "VERSION_CONFLICT", "El profesional cambió. Actualice la lista.");
            version = request.Version + 1;
            Execute(c, t, "UPDATE practitioners SET name=@name,license_ref=@license,active=@active,version=@version WHERE id=@id",
                ("name", request.Name.Trim()), ("license", request.LicenseRef?.Trim()), ("active", request.Active), ("version", version), ("id", id));
        }
        else Execute(c, t, "INSERT INTO practitioners(id,name,license_ref,active) VALUES(@id,@name,@license,@active)",
            ("id", id), ("name", request.Name.Trim()), ("license", request.LicenseRef?.Trim()), ("active", request.Active));
        Execute(c, t, "UPDATE practitioner_services SET active=false WHERE practitioner_id=@id", ("id", id));
        foreach (var service in ids)
            Execute(c, t, """
                INSERT INTO practitioner_services(practitioner_id,service_id) VALUES(@p,@s)
                ON CONFLICT(practitioner_id,service_id) DO UPDATE SET active=true
                """, ("p", id), ("s", service));
        Record(c, t, UserId(User), created ? "catalog.practitioner_created" : "catalog.practitioner_updated", id, HttpContext.TraceIdentifier);
        t.Commit(); return Data(new { id, version }, created ? 201 : 200);
    }
}
