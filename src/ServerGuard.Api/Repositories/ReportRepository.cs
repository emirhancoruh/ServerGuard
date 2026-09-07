using Microsoft.EntityFrameworkCore;
using ServerGuard.Api.Data;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

/// <summary>
/// Rapor özetini üç toplama sorgusuyla hesaplar.
/// </summary>
/// <remarks>
/// Toplama (aggregate) işleri veritabanında yapılır; hiçbir satır belleğe çekilmez.
/// Sorgu aralığı controller tarafında sınırlandığı için tarama miktarı öngörülebilir kalır.
/// Buna ek olarak komutlara bir zaman aşımı verilir: beklenmedik biçimde uzayan bir rapor
/// sorgusu, veri yazan agent'ları süresiz bekletmek yerine iptal edilir.
/// </remarks>
public sealed class ReportRepository(ServerGuardDbContext dbContext) : IReportRepository
{
    /// <summary>Rapor sorgularının aşamayacağı süre.</summary>
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

    public async Task<ReportSummaryDto> GetSummaryAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        dbContext.Database.SetCommandTimeout(QueryTimeout);

        var totalRequestCount = await dbContext.TrafficLogs
            .AsNoTracking()
            .Where(log => log.Timestamp >= from && log.Timestamp < to)
            .Where(log => serverName == null || log.ServerName == serverName)
            .LongCountAsync(cancellationToken);

        // Tek gruba indirgeyip ortalamayı SQL'de aldırıyoruz. Aralıkta hiç ölçüm yoksa
        // grup oluşmaz ve sonuç null döner; ortalamalar sıfırla doldurulmaz.
        var metrics = await dbContext.ServerMetrics
            .AsNoTracking()
            .Where(metric => metric.Timestamp >= from && metric.Timestamp < to)
            .Where(metric => serverName == null || metric.ServerName == serverName)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                SampleCount = group.Count(),
                AverageCpu = (double?)group.Average(metric => metric.CpuUsagePercent),
                AverageRam = (double?)group.Average(metric => metric.RamUsagePercent)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var alertCounts = await dbContext.SecurityAlerts
            .AsNoTracking()
            .Where(alert => alert.Timestamp >= from && alert.Timestamp < to)
            .Where(alert => serverName == null || alert.ServerName == serverName)
            .GroupBy(alert => alert.AlertType)
            .Select(group => new { AlertType = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var alertCountsByType = alertCounts
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.AlertType)
            .Select(item => new AlertTypeCountDto(item.AlertType, item.Count))
            .ToList();

        return new ReportSummaryDto(
            serverName,
            from,
            to,
            totalRequestCount,
            metrics?.AverageCpu,
            metrics?.AverageRam,
            metrics?.SampleCount ?? 0,
            alertCountsByType.Sum(item => item.Count),
            alertCountsByType);
    }
}
