using System.Diagnostics;
using ServerGuard.Shared;

namespace ServerGuard.Agent.Metrics;

/// <summary>
/// Windows PerformanceCounter ile CPU ve fiziksel RAM kullanım yüzdesini okur.
/// </summary>
public sealed class PerformanceCounterMetricsReader : ISystemMetricsReader
{
    private const string ProcessorCategory = "Processor";
    private const string ProcessorTimeCounter = "% Processor Time";
    private const string TotalInstance = "_Total";
    private const string MemoryCategory = "Memory";
    private const string AvailableBytesCounter = "Available Bytes";
    private const double PercentScale = 100d;

    private readonly PerformanceCounter _cpuCounter =
        new(ProcessorCategory, ProcessorTimeCounter, TotalInstance, readOnly: true);

    private readonly PerformanceCounter _availableMemoryCounter =
        new(MemoryCategory, AvailableBytesCounter, readOnly: true);

    private readonly double _totalPhysicalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public PerformanceCounterMetricsReader()
    {
        // CPU sayacının ilk okuması her zaman 0 döner; referans örneği burada alınır.
        _cpuCounter.NextValue();
    }

    public SystemMetricSnapshot Read()
    {
        var cpuPercent = _cpuCounter.NextValue();
        var availableBytes = _availableMemoryCounter.NextValue();
        var usedRatio = (_totalPhysicalMemoryBytes - availableBytes) / _totalPhysicalMemoryBytes;

        return new SystemMetricSnapshot(
            ClampPercent(cpuPercent),
            ClampPercent(usedRatio * PercentScale));
    }

    public void Dispose()
    {
        _cpuCounter.Dispose();
        _availableMemoryCounter.Dispose();
    }

    private static double ClampPercent(double value) =>
        Math.Clamp(value, MetricConstraints.MinPercent, MetricConstraints.MaxPercent);
}
