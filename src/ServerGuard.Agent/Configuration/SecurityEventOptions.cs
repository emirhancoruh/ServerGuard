using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Agent.Configuration;

public sealed class SecurityEventOptions
{
    public const string SectionName = "Agent:SecurityEvents";

    private const string MinInterval = "00:00:01";
    private const string MaxInterval = "00:10:00";

    /// <summary>
    /// Security kanalını okuma yetkisi olmayan makinelerde kapatılabilir.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Kuyruğa biriken olayların backend'e gönderilme sıklığı.</summary>
    [Range(typeof(TimeSpan), MinInterval, MaxInterval)]
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Backend erişilemediğinde yerelde tutulacak en fazla olay sayısı.</summary>
    [Range(QueueCapacityLimits.Min, QueueCapacityLimits.Max)]
    public int QueueCapacity { get; set; } = 5_000;
}
