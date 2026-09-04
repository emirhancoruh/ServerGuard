namespace ServerGuard.Agent.Configuration;

/// <summary>
/// Yerel kuyruk kapasitesi için kabul edilen aralık. Sınırsız kuyruğa izin verilmez.
/// </summary>
public static class QueueCapacityLimits
{
    public const int Min = 1;
    public const int Max = 100_000;
}
