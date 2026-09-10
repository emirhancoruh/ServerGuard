using Microsoft.EntityFrameworkCore;
using ServerGuard.Api.Data;
using ServerGuard.Api.Monitoring;
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
public sealed class ServerRepository(
    ServerGuardDbContext dbContext,
    ServerHealthEvaluator healthEvaluator,
    TimeProvider timeProvider) : IServerRepository
{
    public async Task<IReadOnlyList<ServerSummaryDto>> GetKnownServersAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var lastSeenByServer = await GetLastSeenAsync(since, cancellationToken);

        if (lastSeenByServer.Count == 0)
        {
            return [];
        }

        var latestMetrics = await GetLatestMetricsAsync(since, [.. lastSeenByServer.Keys], cancellationToken);
        var now = timeProvider.GetUtcNow();

        return lastSeenByServer
            .Select(entry => BuildSummary(entry.Key, entry.Value, latestMetrics, now))
            .OrderBy(server => server.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private ServerSummaryDto BuildSummary(
        string serverName,
        DateTimeOffset lastSeenAt,
        IReadOnlyDictionary<string, LatestMetric> latestMetrics,
        DateTimeOffset now)
    {
        // Saat kaymalarında negatif süre oluşmasın diye alt sınır sıfırdır.
        var sinceLastSeen = now > lastSeenAt ? now - lastSeenAt : TimeSpan.Zero;
        var metric = latestMetrics.GetValueOrDefault(serverName);

        return new ServerSummaryDto(
            serverName,
            lastSeenAt,
            healthEvaluator.Evaluate(sinceLastSeen),
            (long)sinceLastSeen.TotalSeconds,
            metric?.CpuUsagePercent,
            metric?.RamUsagePercent,
            metric?.DiskFreePercent);
    }

    private async Task<Dictionary<string, DateTimeOffset>> GetLastSeenAsync(
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

        return lastSeenByServer;
    }

    /// <summary>
    /// Her sunucunun en son metrik satırını getirir. Sunucu sayısı az olduğundan
    /// gruplayıp en yeni zamanı bulmak ve o satırları çekmek yeterlidir.
    /// </summary>
    private async Task<Dictionary<string, LatestMetric>> GetLatestMetricsAsync(
        DateTimeOffset since,
        string[] serverNames,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ServerMetrics
            .AsNoTracking()
            .Where(metric => metric.Timestamp >= since && serverNames.Contains(metric.ServerName))
            .GroupBy(metric => metric.ServerName)
            .Select(group => group
                .OrderByDescending(metric => metric.Timestamp)
                .Select(metric => new LatestMetric(
                    metric.ServerName,
                    metric.CpuUsagePercent,
                    metric.RamUsagePercent,
                    metric.DiskFreePercent))
                .First())
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.ServerName, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record LatestMetric(
        string ServerName,
        double CpuUsagePercent,
        double RamUsagePercent,
        double? DiskFreePercent);
}
