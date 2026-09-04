namespace ServerGuard.Shared;

/// <summary>
/// Agent, Api ve web istemcisi arasındaki endpoint sözleşmesi. Yollar yalnızca buradan okunur.
/// </summary>
public static class ApiRoutes
{
    public const string Health = "/health";
    public const string Servers = "/api/servers";
    public const string Metrics = "/api/metrics";
    public const string SecurityEvents = "/api/security-events";
    public const string Alerts = "/api/alerts";
    public const string TrafficLogs = "/api/traffic";
    public const string MonitoringHub = "/hubs/monitoring";

    /// <summary>Controller içinde kullanılan göreli yol parçaları.</summary>
    public const string TimelineSegment = "timeline";

    public const string TopClientIpsSegment = "top-ips";

    public const string TrafficTimeline = $"{TrafficLogs}/{TimelineSegment}";
    public const string TopClientIps = $"{TrafficLogs}/{TopClientIpsSegment}";
}
