namespace ServerGuard.Agent.Metrics;

/// <summary>
/// Bir anlık kaynak kullanımı ölçümü.
/// </summary>
/// <param name="DiskFreePercent">
/// Sunucudaki en dolu sabit diskin boş alan yüzdesi. Hiçbir disk okunamazsa <c>null</c>.
/// </param>
public sealed record SystemMetricSnapshot(
    double CpuUsagePercent,
    double RamUsagePercent,
    double? DiskFreePercent);
