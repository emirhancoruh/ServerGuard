namespace ServerGuard.Agent.Metrics;

public sealed record SystemMetricSnapshot(double CpuUsagePercent, double RamUsagePercent);
