using ServerGuard.Shared.Enums;

namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Sistemin tanıdığı bir sunucu ve son bilinen durumu.
/// </summary>
/// <param name="LastSeenAt">O sunucudan gelen en son kaydın zamanı.</param>
/// <param name="Status">
/// <paramref name="LastSeenAt"/> üzerinden hesaplanır. Sunucunun ayakta olup olmadığını
/// panelin doğrudan gösterebilmesi için sunucu tarafında belirlenir; istemcilerin saat
/// farkları sonucu değiştirmesin diye.
/// </param>
/// <param name="SecondsSinceLastSeen">Son veriden bu yana geçen süre; panelde "3 dk önce" olarak gösterilir.</param>
/// <param name="CpuUsagePercent">Son ölçüm. Hiç metrik gelmediyse <c>null</c>.</param>
/// <param name="DiskFreePercent">
/// Son ölçümdeki en dolu diskin boş alan yüzdesi. Eski sürüm agent'lar bu alanı
/// göndermediği için <c>null</c> olabilir.
/// </param>
public sealed record ServerSummaryDto(
    string ServerName,
    DateTimeOffset LastSeenAt,
    ServerHealthStatus Status,
    long SecondsSinceLastSeen,
    double? CpuUsagePercent,
    double? RamUsagePercent,
    double? DiskFreePercent);
