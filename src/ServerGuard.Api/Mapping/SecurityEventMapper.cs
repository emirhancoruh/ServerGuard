using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Mapping;

public static class SecurityEventMapper
{
    public static SecurityEvent ToEntity(this SecurityEventDto dto, DateTimeOffset createdAt) => new()
    {
        ServerName = dto.ServerName,
        EventType = dto.EventType,
        SourceIp = dto.SourceIp,
        Username = dto.Username,
        Timestamp = dto.Timestamp,
        CreatedAt = createdAt
    };
}
