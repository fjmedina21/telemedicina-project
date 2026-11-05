using Microsoft.AspNetCore.Mvc;
using Telemedicina.Common.Contracts.Health;
using Telemedicina.Common.Models.ApiResponses;

namespace Telemedicina.Api.Features.Health;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public HealthController(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<HealthCheckResponse>> Get()
    {
        var response = new HealthCheckResponse(
            Status: "Healthy",
            Service: _configuration["App:Name"] ?? "Telemedicina.Api",
            Environment: _environment.EnvironmentName,
            TimestampUtc: DateTime.UtcNow,
            Version: _configuration["App:Version"] ?? "1.0.0"
        );

        return Ok(new ApiResponse<HealthCheckResponse>(StatusCodes.Status200OK, "Service is healthy and operational.", response));
    }
}
