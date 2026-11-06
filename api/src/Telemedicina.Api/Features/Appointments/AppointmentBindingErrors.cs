using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Telemedicina.Api.Shared;
using Telemedicina.Common.Models.ApiResponses;

namespace Telemedicina.Api.Features.Appointments;

/// <summary>Returns safe field identifiers for booking binding failures without echoing submitted values.</summary>
public static class AppointmentBindingErrors
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .Select(entry => entry.Key.Split('.').Last().ToLowerInvariant() switch
            {
                "slotid" => "slotId:INVALID",
                "insurer" => "insurer:INVALID",
                "memberlast4" => "memberLast4:INVALID",
                _ => "request:INVALID"
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var response = new ErrorApiResponse(400,
            "Revise los campos de la solicitud de cita.",
            context.HttpContext.Request.Path.Value ?? "/",
            errors)
        {
            Code = "VALIDATION_FAILED",
            TraceId = context.HttpContext.TraceIdentifier
        };
        return new BadRequestObjectResult(response);
    }
}
