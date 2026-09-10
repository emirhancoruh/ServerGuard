using ServerGuard.Shared.Enums;

namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Panelin en üstünde gösterilen tek bakışta durum özeti.
/// </summary>
/// <remarks>
/// Bir izleme konsolu açıldığında ilk soruya cevap vermelidir: "her şey yolunda mı?"
/// Bu özet o cevabı tek satırda verir; ayrıntıya inmek isteyen aşağıdaki bölümlere bakar.
/// </remarks>
/// <param name="WorstServerStatus">Sunucular arasındaki en kötü durum; başlık rengini belirler.</param>
/// <param name="ErrorRatePercent">Pencere içindeki 5xx oranı.</param>
/// <param name="RequestsPerMinute">Pencere boyunca dakika başına ortalama istek.</param>
public sealed record MonitoringOverviewDto(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalServers,
    int OnlineServers,
    int OfflineServers,
    ServerHealthStatus WorstServerStatus,
    int TotalRequestCount,
    int ClientErrorCount,
    int ServerErrorCount,
    double ErrorRatePercent,
    double RequestsPerMinute,
    double AverageResponseTimeMs,
    long MaxResponseTimeMs,
    int ActiveAlertCount,
    int HighSeverityAlertCount);
