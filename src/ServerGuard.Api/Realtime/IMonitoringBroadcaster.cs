using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Realtime;

/// <summary>
/// Yeni kayıtları bağlı istemcilere duyurur. Asla exception fırlatmaz; yayın hatası kaydı etkilemez.
/// </summary>
public interface IMonitoringBroadcaster
{
    Task BroadcastMetricAsync(ServerMetricDto metric, CancellationToken cancellationToken);

    Task BroadcastSecurityEventAsync(SecurityEventDto securityEvent, CancellationToken cancellationToken);

    Task BroadcastAlertAsync(SecurityAlertDto alert, CancellationToken cancellationToken);

    Task BroadcastTrafficLogAsync(TrafficLogDto trafficLog, CancellationToken cancellationToken);
}
