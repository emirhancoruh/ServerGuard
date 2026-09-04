using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Agent.Configuration;

public sealed class MetricsOptions
{
    public const string SectionName = "Agent:Metrics";

    private const string MinInterval = "00:00:01";
    private const string MaxInterval = "01:00:00";

    public bool Enabled { get; set; } = true;

    [Range(typeof(TimeSpan), MinInterval, MaxInterval)]
    public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Backend erişilemediğinde yerelde tutulacak en fazla metrik sayısı.</summary>
    [Range(QueueCapacityLimits.Min, QueueCapacityLimits.Max)]
    public int QueueCapacity { get; set; } = 1_000;
}
