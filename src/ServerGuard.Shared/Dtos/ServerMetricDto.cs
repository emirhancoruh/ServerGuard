namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Bir sunucunun anlık kaynak kullanımı.
/// </summary>
/// <param name="DiskFreePercent">
/// Sunucudaki en dolu sabit diskin boş alan yüzdesi. Disk dolması, sunucu çökmelerinin
/// en yaygın sebeplerinden biridir. Alan <b>nullable</b>'dır: bu ölçümü göndermeyen eski
/// sürüm agent'lar da çalışmaya devam edebilsin diye.
/// </param>
public sealed record ServerMetricDto(
    string ServerName,
    double CpuUsagePercent,
    double RamUsagePercent,
    DateTimeOffset Timestamp,
    double? DiskFreePercent = null) : IServerPayload;
