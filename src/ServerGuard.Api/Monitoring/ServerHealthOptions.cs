using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Api.Monitoring;

/// <summary>
/// Bir sunucunun ne zaman "gecikmiş" ne zaman "erişilemez" sayılacağını belirler.
/// </summary>
/// <remarks>
/// Eşikler agent'ın toplama aralığına göre ayarlanmalıdır. Varsayılan aralık 10 saniyeyken
/// 60 saniye sessizlik gecikme, 3 dakika sessizlik erişilemezlik sayılır; böylece tek bir
/// kaçırılmış gönderim yanlış alarm üretmez.
/// </remarks>
public sealed class ServerHealthOptions
{
    public const string SectionName = "Monitoring:ServerHealth";

    private const string MinThreshold = "00:00:15";
    private const string MaxThreshold = "24:00:00";

    [Range(typeof(TimeSpan), MinThreshold, MaxThreshold)]
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(60);

    [Range(typeof(TimeSpan), MinThreshold, MaxThreshold)]
    public TimeSpan OfflineAfter { get; set; } = TimeSpan.FromMinutes(3);
}
