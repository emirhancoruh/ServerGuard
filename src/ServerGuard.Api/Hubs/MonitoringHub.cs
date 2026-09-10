using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ServerGuard.Api.Security;

namespace ServerGuard.Api.Hubs;

/// <summary>
/// Canlı monitoring verisinin yayınlandığı hub. Strongly-typed olduğundan event adlarında magic string yoktur.
/// </summary>
/// <remarks>
/// Hub, alarmlar ve trafik dahil tüm canlı veriyi yayınladığından REST uçlarıyla aynı
/// yetkiyi ister. Tarayıcı WebSocket'te header gönderemediğinden token sorgu parametresiyle
/// taşınır; bu kabul yalnızca hub yoluna tanınmıştır.
/// </remarks>
[Authorize(Policy = AuthorizationPolicies.Panel)]
public sealed class MonitoringHub(ILogger<MonitoringHub> logger) : Hub<IMonitoringClient>
{
    public override Task OnConnectedAsync()
    {
        logger.LogDebug("Monitoring client connected. ConnectionId={ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogDebug(exception, "Monitoring client disconnected. ConnectionId={ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
