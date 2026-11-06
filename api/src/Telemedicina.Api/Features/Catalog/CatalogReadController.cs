using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telemedicina.Api.Features.Scheduling;
using Telemedicina.Api.Infrastructure.Persistence;
using Telemedicina.Api.Shared;
using Telemedicina.Common.Models.ApiResponses;
using static Telemedicina.Api.Infrastructure.Persistence.Database;

namespace Telemedicina.Api.Features.Catalog;

[ApiController, Authorize(Roles = "Patient,Admission"), Route("api/v1")]
[ProducesResponseType(typeof(ErrorApiResponse), 401)]
[ProducesResponseType(typeof(ErrorApiResponse), 403)]
public sealed class CatalogReadController(Database database) : ApiControllerBase(database)
{
    [HttpGet("specialties")]
    [ProducesResponseType(typeof(ApiResponse<List<SpecialtySummary>>), 200)]
    public IActionResult Specialties()
    {
        using var c = Database.Open();
        return Data(Rows(c, null, "SELECT DISTINCT v.specialty AS name FROM services v WHERE " + OfferPolicy.PublicServiceSql + " ORDER BY name"));
    }
    [HttpGet("services")]
    [ProducesResponseType(typeof(ApiResponse<List<ServiceSummary>>), 200)]
    public IActionResult Services([FromQuery] string? specialty = null)
    {
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT v.id,v.name,v.specialty,v.duration AS "durationMinutes" FROM services v WHERE
            """ + " " + OfferPolicy.PublicServiceSql + " AND (@s='' OR v.specialty=@s) ORDER BY v.name,v.id", ("s", specialty ?? "")));
    }
    [HttpGet("practitioners")]
    [ProducesResponseType(typeof(ApiResponse<List<PractitionerSummary>>), 200)]
    [ProducesResponseType(typeof(ErrorApiResponse), 400)]
    public IActionResult Practitioners([FromQuery] string? serviceId = null)
    {
        if (!ScheduleInputs.TryId(serviceId, false, out var service)) return Fail(400, "VALIDATION_FAILED", "Seleccione un servicio válido.");
        using var c = Database.Open();
        return Data(Rows(c, null, """
            SELECT p.id,p.name FROM practitioners p WHERE p.active AND (@sid='' OR EXISTS(
                SELECT 1 FROM practitioner_services ps JOIN services v ON v.id=ps.service_id
                WHERE ps.practitioner_id=p.id AND ps.service_id=@sid AND ps.active AND
            """ + " " + OfferPolicy.PublicServiceSql + ")) ORDER BY p.name,p.id", ("sid", service)));
    }
}
