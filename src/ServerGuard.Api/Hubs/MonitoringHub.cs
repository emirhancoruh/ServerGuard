using Microsoft.AspNetCore.SignalR;

namespace ServerGuard.Api.Hubs;

/// <summary>
/// Canlı monitoring verisinin yayınlandığı hub. Strongly-typed olduğundan event adlarında magic string yoktur.
/// </summary>
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
