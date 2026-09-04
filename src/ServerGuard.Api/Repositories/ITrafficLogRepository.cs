using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

public interface ITrafficLogRepository
{
    Task<TrafficLog> AddAsync(TrafficLog trafficLog, CancellationToken cancellationToken);

    /// <summary>
    /// Verilen aralıktaki istekleri eşit dilimlere bölerek sayar.
    /// İstek gelmeyen dilimler sıfır sayacıyla doldurulur.
    /// </summary>
    Task<IReadOnlyList<TrafficTimelinePointDto>> GetTimelineAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        int bucketSeconds,
        CancellationToken cancellationToken);

    /// <summary>Verilen aralıkta en çok istek gönderen adresleri döner.</summary>
    Task<IReadOnlyList<TopClientIpDto>> GetTopClientIpsAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        int take,
        CancellationToken cancellationToken);
}
