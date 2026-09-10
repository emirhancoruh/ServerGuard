using Microsoft.Extensions.Options;
using ServerGuard.Shared.Enums;

namespace ServerGuard.Api.Monitoring;

/// <summary>
/// Son görülme zamanından sunucu durumunu belirler.
/// </summary>
/// <remarks>
/// Karar sunucu tarafında verilir: istemcilerin saatleri kaymış olabilir ve aynı sunucu
/// iki farklı panelde farklı görünmemelidir.
/// </remarks>
public sealed class ServerHealthEvaluator(IOptions<ServerHealthOptions> options)
{
    private readonly ServerHealthOptions _options = options.Value;

    public ServerHealthStatus Evaluate(TimeSpan sinceLastSeen) => sinceLastSeen switch
    {
        _ when sinceLastSeen >= _options.OfflineAfter => ServerHealthStatus.Offline,
        _ when sinceLastSeen >= _options.StaleAfter => ServerHealthStatus.Stale,
        _ => ServerHealthStatus.Online
    };
}
