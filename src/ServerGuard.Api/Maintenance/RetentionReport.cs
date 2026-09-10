namespace ServerGuard.Api.Maintenance;

/// <summary>Bir temizlik turunda tablo başına silinen satır sayısı.</summary>
public sealed record RetentionReport(
    int ServerMetrics,
    int TrafficLogs,
    int SecurityEvents,
    int SecurityAlerts)
{
    public int Total => ServerMetrics + TrafficLogs + SecurityEvents + SecurityAlerts;
}
