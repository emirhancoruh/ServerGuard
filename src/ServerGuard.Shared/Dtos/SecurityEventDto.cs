using ServerGuard.Shared.Enums;

namespace ServerGuard.Shared.Dtos;

public sealed record SecurityEventDto(
    string ServerName,
    SecurityEventType EventType,
    string SourceIp,
    string Username,
    DateTimeOffset Timestamp) : IServerPayload;
