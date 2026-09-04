namespace ServerGuard.Shared.Dtos;

public sealed record ServerMetricDto(
    string ServerName,
    double CpuUsagePercent,
    double RamUsagePercent,
    DateTimeOffset Timestamp) : IServerPayload;
