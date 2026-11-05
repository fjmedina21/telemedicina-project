namespace Telemedicina.Common.Contracts.Health;

public record HealthCheckResponse(
    string Status,
    string Service,
    string Environment,
    DateTime TimestampUtc,
    string Version);
