using Microsoft.EntityFrameworkCore;
using ServerGuard.Api.Data;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

public sealed class TrafficLogRepository(ServerGuardDbContext dbContext) : ITrafficLogRepository
{
    private const int ClientErrorLowerBound = 400;
    private const int ServerErrorLowerBound = 500;
    private const double FullPercent = 100;

    public async Task<TrafficLog> AddAsync(TrafficLog trafficLog, CancellationToken cancellationToken)
    {
        dbContext.TrafficLogs.Add(trafficLog);
        await dbContext.SaveChangesAsync(cancellationToken);
        return trafficLog;
    }

    public async Task<IReadOnlyList<TrafficTimelinePointDto>> GetTimelineAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        int bucketSeconds,
        CancellationToken cancellationToken)
    {
        // Gruplama veritabanında yapılır; tüm satırları belleğe çekmek yerine yalnızca dilim sayaçları gelir.
        var counts = await ApplyFilters(dbContext.TrafficLogs.AsNoTracking(), serverName, from, to)
            .GroupBy(log => EF.Functions.DateDiffSecond(from, log.Timestamp) / bucketSeconds)
            .Select(group => new BucketCounts(
                group.Key,
                group.Count(),
                group.Count(log => log.StatusCode < ClientErrorLowerBound),
                group.Count(log => log.StatusCode >= ClientErrorLowerBound && log.StatusCode < ServerErrorLowerBound),
                group.Count(log => log.StatusCode >= ServerErrorLowerBound)))
            .ToListAsync(cancellationToken);

        return BuildContinuousSeries(
            counts.ToDictionary(item => item.BucketIndex),
            from,
            to,
            bucketSeconds);
    }

    public async Task<IReadOnlyList<TopClientIpDto>> GetTopClientIpsAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        int take,
        CancellationToken cancellationToken)
    {
        // Sıralama ve sayfalama veritabanında yapılır. Projeksiyon anonim tipe yapılıyor;
        // EF, record'a çevrilmiş bir ifadenin alanlarına göre sıralamayı SQL'e çeviremiyor.
        var ranked = await ApplyFilters(dbContext.TrafficLogs.AsNoTracking(), serverName, from, to)
            .GroupBy(log => log.ClientIp)
            .Select(group => new { ClientIp = group.Key, RequestCount = group.Count() })
            .OrderByDescending(item => item.RequestCount)
            // Eşit sayıda isteği olan adreslerde sıralamanın kararlı kalması için ikincil anahtar.
            .ThenBy(item => item.ClientIp)
            .Take(take)
            .ToListAsync(cancellationToken);

        return ranked.ConvertAll(item => new TopClientIpDto(item.ClientIp, item.RequestCount));
    }

    public async Task<IReadOnlyList<ServiceHealthDto>> GetServiceHealthAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        // Toplama veritabanında yol bazında yapılır; servis adına indirgeme bellekte tamamlanır.
        // Yolun N'inci eğik çizgisini bulmak SQL'e çevrilemediğinden ön ek burada çıkarılır.
        // Farklı yol sayısı gruplama sonrası sınırlı kaldığı için bu maliyet düşüktür.
        var byPath = await ApplyFilters(dbContext.TrafficLogs.AsNoTracking(), serverName, from, to)
            .GroupBy(log => log.RequestPath)
            .Select(group => new PathAggregate(
                group.Key,
                group.Count(),
                group.Count(log => log.StatusCode >= ClientErrorLowerBound && log.StatusCode < ServerErrorLowerBound),
                group.Count(log => log.StatusCode >= ServerErrorLowerBound),
                group.Sum(log => log.ResponseTimeMs),
                group.Max(log => log.ResponseTimeMs)))
            .ToListAsync(cancellationToken);

        return byPath
            .GroupBy(item => ToServiceName(item.RequestPath), StringComparer.OrdinalIgnoreCase)
            .Select(ToServiceHealth)
            .OrderByDescending(service => service.ServerErrorCount)
            .ThenByDescending(service => service.RequestCount)
            .ThenBy(service => service.ServiceName, StringComparer.OrdinalIgnoreCase)
            .Take(MonitoringConstraints.MaxServiceRows)
            .ToList();
    }

    public async Task<TrafficTotals> GetTotalsAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        // Tek gruba indirgeyip toplamları SQL'de aldırıyoruz. Hiç kayıt yoksa grup oluşmaz
        // ve sonuç null döner; sayaçlar sıfır kabul edilir.
        var totals = await ApplyFilters(dbContext.TrafficLogs.AsNoTracking(), serverName, from, to)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                RequestCount = group.Count(),
                ClientErrorCount = group.Count(log => log.StatusCode >= ClientErrorLowerBound && log.StatusCode < ServerErrorLowerBound),
                ServerErrorCount = group.Count(log => log.StatusCode >= ServerErrorLowerBound),
                TotalResponseTimeMs = group.Sum(log => log.ResponseTimeMs),
                MaxResponseTimeMs = group.Max(log => log.ResponseTimeMs)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (totals is null || totals.RequestCount == 0)
        {
            return new TrafficTotals(0, 0, 0, 0, 0);
        }

        return new TrafficTotals(
            totals.RequestCount,
            totals.ClientErrorCount,
            totals.ServerErrorCount,
            (double)totals.TotalResponseTimeMs / totals.RequestCount,
            totals.MaxResponseTimeMs);
    }

    private static ServiceHealthDto ToServiceHealth(IGrouping<string, PathAggregate> group)
    {
        var requestCount = group.Sum(item => item.RequestCount);
        var serverErrorCount = group.Sum(item => item.ServerErrorCount);
        var totalResponseTimeMs = group.Sum(item => item.TotalResponseTimeMs);

        return new ServiceHealthDto(
            group.Key,
            requestCount,
            group.Sum(item => item.ClientErrorCount),
            serverErrorCount,
            requestCount == 0 ? 0 : serverErrorCount * FullPercent / requestCount,
            requestCount == 0 ? 0 : (double)totalResponseTimeMs / requestCount,
            group.Max(item => item.MaxResponseTimeMs));
    }

    /// <summary>
    /// İstek yolunun ilk birkaç segmentini servis adı olarak kullanır
    /// (ör. <c>/services/kanban/Sync/Webhook</c> → <c>/services/kanban</c>).
    /// </summary>
    private static string ToServiceName(string requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            return MonitoringConstraints.UnknownServiceName;
        }

        var segments = requestPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Length == 0
            ? "/"
            : "/" + string.Join('/', segments.Take(MonitoringConstraints.ServiceNameSegmentCount));
    }

    private static IQueryable<TrafficLog> ApplyFilters(
        IQueryable<TrafficLog> source,
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        source = source.Where(log => log.Timestamp >= from && log.Timestamp < to);

        return string.IsNullOrWhiteSpace(serverName)
            ? source
            : source.Where(log => log.ServerName == serverName);
    }

    /// <summary>
    /// Boş dilimleri sıfırla doldurur. Aksi halde çizgi grafik, istek gelmeyen aralıkları
    /// atlayarak yanıltıcı bir süreklilik gösterirdi.
    /// </summary>
    private static List<TrafficTimelinePointDto> BuildContinuousSeries(
        Dictionary<int, BucketCounts> countsByBucket,
        DateTimeOffset from,
        DateTimeOffset to,
        int bucketSeconds)
    {
        var bucketCount = (int)Math.Ceiling((to - from).TotalSeconds / bucketSeconds);
        var series = new List<TrafficTimelinePointDto>(bucketCount);

        for (var index = 0; index < bucketCount; index++)
        {
            var timestamp = from.AddSeconds((long)index * bucketSeconds);
            var counts = countsByBucket.GetValueOrDefault(index);

            series.Add(new TrafficTimelinePointDto(
                timestamp,
                counts?.RequestCount ?? 0,
                counts?.SuccessCount ?? 0,
                counts?.ClientErrorCount ?? 0,
                counts?.ServerErrorCount ?? 0));
        }

        return series;
    }

    private sealed record BucketCounts(
        int BucketIndex,
        int RequestCount,
        int SuccessCount,
        int ClientErrorCount,
        int ServerErrorCount);

    private sealed record PathAggregate(
        string RequestPath,
        int RequestCount,
        int ClientErrorCount,
        int ServerErrorCount,
        long TotalResponseTimeMs,
        long MaxResponseTimeMs);
}
