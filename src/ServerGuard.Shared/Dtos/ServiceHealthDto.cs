namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Tek bir servisin (istek yolu ön eki) sağlık özeti.
/// </summary>
/// <param name="ServiceName">İstek yolundan türetilen ön ek, ör. <c>/services/kanban</c>.</param>
/// <param name="ErrorRatePercent">5xx oranı. Hangi servisin bozulduğunu gösteren asıl sinyal.</param>
/// <param name="AverageResponseTimeMs">Ortalama yanıt süresi.</param>
/// <param name="MaxResponseTimeMs">En yavaş istek; ortalamanın gizlediği uç durumları açığa çıkarır.</param>
public sealed record ServiceHealthDto(
    string ServiceName,
    int RequestCount,
    int ClientErrorCount,
    int ServerErrorCount,
    double ErrorRatePercent,
    double AverageResponseTimeMs,
    long MaxResponseTimeMs);
