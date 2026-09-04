namespace ServerGuard.Shared.Dtos;

public sealed record TrafficLogDto(
    string ServerName,
    string ClientIp,
    string RequestPath,
    int StatusCode,
    long ResponseTimeMs,
    DateTimeOffset Timestamp) : IServerPayload;
