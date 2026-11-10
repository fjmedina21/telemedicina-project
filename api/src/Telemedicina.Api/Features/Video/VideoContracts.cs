namespace Telemedicina.Api.Features.Video;

public sealed record VideoTokenResponse(string SessionId, string Status, string Token,
    string Url, DateTime ExpiresAt);
public sealed record VideoStatusResponse(string SessionId, string Status,
    DateTime? StartedAt, DateTime? EndedAt, int ConnectedParticipants);
public sealed record VideoConnectionEventRequest(string Type);
