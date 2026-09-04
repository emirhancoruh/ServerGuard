using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Hubs;

/// <summary>
/// Sunucudan istemciye giden SignalR event'leri. Metot adı istemcide dinlenen event adıdır.
/// </summary>
public interface IMonitoringClient
{
    Task ReceiveMetric(ServerMetricDto metric);

    Task ReceiveSecurityEvent(SecurityEventDto securityEvent);

    Task ReceiveAlert(SecurityAlertDto alert);

    Task ReceiveTrafficLog(TrafficLogDto trafficLog);
}
