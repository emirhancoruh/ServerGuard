using ServerGuard.Api.Contracts;
using ServerGuard.Api.Repositories;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;
using ServerGuard.Shared.Enums;

namespace ServerGuard.Api.Monitoring;

/// <summary>
/// Sunucu durumları, trafik sayaçları ve alarmları tek bir özete indirger.
/// </summary>
/// <remarks>
/// Bu hesabı istemciye bırakmak, her panelin kendi yorumunu üretmesi ve aynı sistemin
/// iki ekranda farklı görünmesi anlamına gelirdi. Karar tek yerde verilir.
/// </remarks>
public sealed class MonitoringOverviewService(
    IServerRepository serverRepository,
    ITrafficLogRepository trafficRepository,
    ISecurityAlertRepository alertRepository,
    TimeProvider timeProvider) : IMonitoringOverviewService
{
    private const double FullPercent = 100;

    public async Task<MonitoringOverviewDto> GetOverviewAsync(
        string? serverName,
        int minutes,
        CancellationToken cancellationToken)
    {
        var to = timeProvider.GetUtcNow();
        var from = to.AddMinutes(-minutes);

        // Sunucu listesi, kısa pencereden bağımsız olarak "erişilemez" kararını verebilmek için
        // daha geniş bir aralıktan çekilir; aksi halde susmuş bir sunucu listeden tamamen düşer
        // ve panelde hiç görünmezdi.
        var serverLookback = to.AddHours(-ServerQueryConstraints.DefaultSinceHours);
        var servers = await serverRepository.GetKnownServersAsync(serverLookback, cancellationToken);

        if (!string.IsNullOrWhiteSpace(serverName))
        {
            servers = servers
                .Where(server => string.Equals(server.ServerName, serverName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totals = await trafficRepository.GetTotalsAsync(serverName, from, to, cancellationToken);

        var alerts = await alertRepository.QueryAsync(
            new AlertQuery
            {
                ServerName = serverName,
                From = from,
                To = to,
                Page = PaginationConstraints.MinPage,
                PageSize = PaginationConstraints.MaxPageSize
            },
            cancellationToken);

        return new MonitoringOverviewDto(
            from,
            to,
            servers.Count,
            servers.Count(server => server.Status == ServerHealthStatus.Online),
            servers.Count(server => server.Status == ServerHealthStatus.Offline),
            WorstStatusOf(servers),
            totals.RequestCount,
            totals.ClientErrorCount,
            totals.ServerErrorCount,
            totals.RequestCount == 0 ? 0 : totals.ServerErrorCount * FullPercent / totals.RequestCount,
            minutes == 0 ? 0 : (double)totals.RequestCount / minutes,
            totals.AverageResponseTimeMs,
            totals.MaxResponseTimeMs,
            alerts.TotalCount,
            alerts.Items.Count(alert => alert.Severity >= AlertSeverity.High));
    }

    /// <summary>
    /// Sunucular arasındaki en kötü durum; başlık rengi buna göre belirlenir.
    /// Hiç sunucu yoksa veri akmıyor demektir, bu da <see cref="ServerHealthStatus.Offline"/> sayılır.
    /// </summary>
    private static ServerHealthStatus WorstStatusOf(IReadOnlyList<ServerSummaryDto> servers) =>
        servers.Count == 0
            ? ServerHealthStatus.Offline
            : servers.Max(server => server.Status);
}
