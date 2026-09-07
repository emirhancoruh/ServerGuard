using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Repositories;

public interface IReportRepository
{
    /// <summary>
    /// Verilen aralığın özetini döner: toplam istek, ortalama CPU/RAM ve tipe göre alarm kırılımı.
    /// </summary>
    Task<ReportSummaryDto> GetSummaryAsync(
        string? serverName,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken);
}
