using ServerGuard.Shared.Enums;

namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Belirli bir tarih aralığının özeti.
/// </summary>
/// <param name="ServerName">Filtrelenen sunucu; <c>null</c> ise tüm sunucular.</param>
/// <param name="AverageCpuUsagePercent">
/// Aralıkta hiç metrik toplanmadıysa <c>null</c>. "Ölçüm yok" ile "ortalama sıfır"
/// farklı anlamlar taşıdığı için sıfırla doldurulmaz.
/// </param>
/// <param name="MetricSampleCount">Ortalamaların kaç ölçüme dayandığı; güvenilirliğin göstergesi.</param>
public sealed record ReportSummaryDto(
    string? ServerName,
    DateTimeOffset From,
    DateTimeOffset To,
    long TotalRequestCount,
    double? AverageCpuUsagePercent,
    double? AverageRamUsagePercent,
    int MetricSampleCount,
    int TotalAlertCount,
    IReadOnlyList<AlertTypeCountDto> AlertCountsByType);

/// <summary>Alarm tipine göre kırılım.</summary>
public sealed record AlertTypeCountDto(AlertType AlertType, int Count);
