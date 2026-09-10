namespace ServerGuard.Api.Data.Entities;

/// <summary>
/// ServerMetricDto'nun veritabanı karşılığı. Id ve CreatedAt yalnızca kalıcı katmana aittir.
/// </summary>
public sealed class ServerMetric
{
    public long Id { get; init; }
    public required string ServerName { get; init; }
    public required double CpuUsagePercent { get; init; }
    public required double RamUsagePercent { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// En dolu diskin boş alan yüzdesi. Bu ölçümü göndermeyen eski agent'lar için <c>null</c>.
    /// </summary>
    public double? DiskFreePercent { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
