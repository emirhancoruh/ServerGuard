using System.Text.Json.Serialization;
using ServerGuard.Api.Hubs;
using ServerGuard.Shared;

namespace ServerGuard.Api.Realtime;

public static class RealtimeExtensions
{
    public static IServiceCollection AddRealtime(this IServiceCollection services)
    {
        // Hub yayınları da REST yanıtlarıyla aynı sözleşmeyi kullanır: enum'lar ad olarak gider.
        services
            .AddSignalR()
            .AddJsonProtocol(options =>
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddSingleton<IMonitoringBroadcaster, SignalRMonitoringBroadcaster>();
        return services;
    }

    public static IEndpointRouteBuilder MapRealtimeHubs(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<MonitoringHub>(ApiRoutes.MonitoringHub);
        return endpoints;
    }
}
