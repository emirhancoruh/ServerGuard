using Microsoft.EntityFrameworkCore;
using ServerGuard.Api.Data;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

/// <summary>
/// Sunucu listesini gelen kayıtlardan türetir; ayrı bir sunucu kayıt tablosu tutulmaz.
/// </summary>
/// <remarks>
/// Hem metrik hem trafik kayıtları taranır: bir sunucuda metrik toplama kapatılmış olsa bile
/// trafik gönderiyorsa listede görünür. Her iki sorgu da <c>(ServerName, Timestamp)</c> index'ini
/// kullanır ve sunucu başına tek satır döndürür.
/// </remarks>
public sealed class ServerRepository(ServerGuardDbContext dbContext) : IServerRepository
{
    public async Task<IReadOnlyList<ServerSummaryDto>> GetKnownServersAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var fromMetrics = await dbContext.ServerMetrics
            .AsNoTracking()
            .Where(metric => metric.Timestamp >= since)
            .GroupBy(metric => metric.ServerName)
            .Select(group => new { ServerName = group.Key, LastSeenAt = group.Max(item => item.Timestamp) })
            .ToListAsync(cancellationToken);

        var fromTraffic = await dbContext.TrafficLogs
            .AsNoTracking()
            .Where(log => log.Timestamp >= since)
            .GroupBy(log => log.ServerName)
            .Select(group => new { ServerName = group.Key, LastSeenAt = group.Max(item => item.Timestamp) })
            .ToListAsync(cancellationToken);

        var lastSeenByServer = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in fromMetrics.Concat(fromTraffic))
        {
            if (!lastSeenByServer.TryGetValue(entry.ServerName, out var current) || entry.LastSeenAt > current)
            {
                lastSeenByServer[entry.ServerName] = entry.LastSeenAt;
            }
        }

        return lastSeenByServer
            .Select(entry => new ServerSummaryDto(entry.Key, entry.Value))
            .OrderBy(server => server.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
