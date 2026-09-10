using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

public interface ITrafficLogRepository
{
    Task<TrafficLog> AddAsync(TrafficLog trafficLog, CancellationToken cancellationToken);

    /// <summary>
    /// Verilen aralıktaki istekleri eşit dilimlere bölerek HTTP durum sınıfına göre sayar.
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

    /// <summary>
    /// İstek yolu ön ekine göre servis bazında sağlık özeti döner; en bozuk servis başta olur.
    /// </summary>
    Task<IReadOnlyList<ServiceHealthDto>> GetServiceHealthAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);

    /// <summary>Verilen aralığın toplam trafik istatistikleri.</summary>
    Task<TrafficTotals> GetTotalsAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}

/// <summary>Genel bakış için toplanmış trafik sayaçları.</summary>
public sealed record TrafficTotals(
    int RequestCount,
    int ClientErrorCount,
    int ServerErrorCount,
    double AverageResponseTimeMs,
    long MaxResponseTimeMs);
