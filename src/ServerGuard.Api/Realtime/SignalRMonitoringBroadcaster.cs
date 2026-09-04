using Microsoft.AspNetCore.SignalR;
using ServerGuard.Api.Hubs;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Realtime;

/// <summary>
/// SignalR üzerinden yayın yapar. SignalR her bağlantıya bağımsız gönderir; kopmuş bir istemci
/// diğerlerini etkilemez. Buradaki try/catch ise hub altyapısındaki bir hatanın çağıran işlemi
/// düşürmesini engeller — kayıt zaten veritabanına yazılmıştır.
/// </summary>
public sealed class SignalRMonitoringBroadcaster(
    IHubContext<MonitoringHub, IMonitoringClient> hubContext,
    ILogger<SignalRMonitoringBroadcaster> logger) : IMonitoringBroadcaster
{
    public Task BroadcastMetricAsync(ServerMetricDto metric, CancellationToken cancellationToken) =>
        BroadcastAsync(metric, client => client.ReceiveMetric(metric), cancellationToken);

    public Task BroadcastSecurityEventAsync(SecurityEventDto securityEvent, CancellationToken cancellationToken) =>
        BroadcastAsync(securityEvent, client => client.ReceiveSecurityEvent(securityEvent), cancellationToken);

    public Task BroadcastAlertAsync(SecurityAlertDto alert, CancellationToken cancellationToken) =>
        BroadcastAsync(alert, client => client.ReceiveAlert(alert), cancellationToken);

    public Task BroadcastTrafficLogAsync(TrafficLogDto trafficLog, CancellationToken cancellationToken) =>
        BroadcastAsync(trafficLog, client => client.ReceiveTrafficLog(trafficLog), cancellationToken);

    private async Task BroadcastAsync(
        IServerPayload payload,
        Func<IMonitoringClient, Task> send,
        CancellationToken cancellationToken)
    {
        try
        {
            await send(hubContext.Clients.All);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Broadcast failed; record is already persisted. Server={ServerName} Timestamp={Timestamp}",
                payload.ServerName,
                payload.Timestamp);
        }
    }
}
