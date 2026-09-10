using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Monitoring;

/// <summary>
/// Panelin üst durum çubuğunu besleyen özeti hazırlar.
/// </summary>
public interface IMonitoringOverviewService
{
    Task<MonitoringOverviewDto> GetOverviewAsync(
        string? serverName,
        int minutes,
        CancellationToken cancellationToken);
}
