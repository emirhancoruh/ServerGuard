using Microsoft.EntityFrameworkCore;
using ServerGuard.Api.Data;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

public sealed class TrafficLogRepository(ServerGuardDbContext dbContext) : ITrafficLogRepository
{
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
            .Select(group => new { BucketIndex = group.Key, RequestCount = group.Count() })
            .ToListAsync(cancellationToken);

        return BuildContinuousSeries(counts.ToDictionary(item => item.BucketIndex, item => item.RequestCount), from, to, bucketSeconds);
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
        Dictionary<int, int> countsByBucket,
        DateTimeOffset from,
        DateTimeOffset to,
        int bucketSeconds)
    {
        var bucketCount = (int)Math.Ceiling((to - from).TotalSeconds / bucketSeconds);
        var series = new List<TrafficTimelinePointDto>(bucketCount);

        for (var index = 0; index < bucketCount; index++)
        {
            series.Add(new TrafficTimelinePointDto(
                from.AddSeconds((long)index * bucketSeconds),
                countsByBucket.GetValueOrDefault(index)));
        }

        return series;
    }
}
