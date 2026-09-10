using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Mapping;

public static class ServerMetricMapper
{
    public static ServerMetric ToEntity(this ServerMetricDto dto, DateTimeOffset createdAt) => new()
    {
        ServerName = dto.ServerName,
        CpuUsagePercent = dto.CpuUsagePercent,
        RamUsagePercent = dto.RamUsagePercent,
        Timestamp = dto.Timestamp,
        DiskFreePercent = dto.DiskFreePercent,
        CreatedAt = createdAt
    };
}
