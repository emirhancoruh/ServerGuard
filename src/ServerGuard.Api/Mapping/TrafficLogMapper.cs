using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Mapping;

public static class TrafficLogMapper
{
    public static TrafficLog ToEntity(this TrafficLogDto dto, DateTimeOffset createdAt) => new()
    {
        ServerName = dto.ServerName,
        ClientIp = dto.ClientIp,
        RequestPath = dto.RequestPath,
        StatusCode = dto.StatusCode,
        ResponseTimeMs = dto.ResponseTimeMs,
        Timestamp = dto.Timestamp,
        CreatedAt = createdAt
    };
}
