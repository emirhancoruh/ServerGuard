using System.Diagnostics;
using ServerGuard.Shared;

namespace ServerGuard.Agent.Metrics;

/// <summary>
/// Windows PerformanceCounter ile CPU ve fiziksel RAM kullanım yüzdesini,
/// <see cref="DriveInfo"/> ile de disk doluluğunu okur.
/// </summary>
public sealed class PerformanceCounterMetricsReader(ILogger<PerformanceCounterMetricsReader> logger) : ISystemMetricsReader
{
    private const string ProcessorCategory = "Processor";
    private const string ProcessorTimeCounter = "% Processor Time";
    private const string TotalInstance = "_Total";
    private const string MemoryCategory = "Memory";
    private const string AvailableBytesCounter = "Memory";
    private const double PercentScale = 100d;

    private readonly PerformanceCounter _cpuCounter = CreateWarmedCpuCounter();

    private readonly PerformanceCounter _availableMemoryCounter =
        new(MemoryCategory, "Available Bytes", readOnly: true);

    private readonly double _totalPhysicalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public SystemMetricSnapshot Read()
    {
        var cpuPercent = _cpuCounter.NextValue();
        var availableBytes = _availableMemoryCounter.NextValue();
        var usedRatio = (_totalPhysicalMemoryBytes - availableBytes) / _totalPhysicalMemoryBytes;

        return new SystemMetricSnapshot(
            ClampPercent(cpuPercent),
            ClampPercent(usedRatio * PercentScale),
            ReadLowestFreeDiskPercent());
    }

    public void Dispose()
    {
        _cpuCounter.Dispose();
        _availableMemoryCounter.Dispose();
    }

    private static PerformanceCounter CreateWarmedCpuCounter()
    {
        var counter = new PerformanceCounter(ProcessorCategory, ProcessorTimeCounter, TotalInstance, readOnly: true);

        // CPU sayacının ilk okuması her zaman 0 döner; referans örneği burada alınır.
        counter.NextValue();

        return counter;
    }

    /// <summary>
    /// Sabit diskler arasında <b>en az boş alanı</b> olanın yüzdesini döner.
    /// En kötü diski raporlamak doğru olandır: sunucuyu durduran, ortalama değil dolan diskdir.
    /// </summary>
    private double? ReadLowestFreeDiskPercent()
    {
        try
        {
            var lowest = DriveInfo.GetDrives()
                .Where(drive => drive is { DriveType: DriveType.Fixed, IsReady: true, TotalSize: > 0 })
                .Select(drive => (double)drive.TotalFreeSpace / drive.TotalSize * PercentScale)
                .DefaultIfEmpty(double.NaN)
                .Min();

            return double.IsNaN(lowest) ? null : ClampPercent(lowest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Disk okunamazsa metrik yine gönderilir, yalnızca bu alan boş kalır.
            logger.LogWarning(
                "Disk usage could not be read ({Reason}: {Message}); the metric will be sent without it.",
                exception.GetType().Name,
                exception.Message);

            return null;
        }
    }

    private static double ClampPercent(double value) =>
        Math.Clamp(value, MetricConstraints.MinPercent, MetricConstraints.MaxPercent);
}
