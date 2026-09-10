using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Api.Throttling;

/// <summary>
/// İstek hızı sınırları. Amaç kötü niyetli bir seli tamamen durdurmak değil, tek bir
/// istemcinin API'yi ve veritabanını tüketmesini engellemektir.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    private const int MinPermitLimit = 1;
    private const int MaxPermitLimit = 1_000_000;
    private const int MinQueueLimit = 0;
    private const int MaxQueueLimit = 10_000;

    /// <summary>Kapatıldığında hiçbir sınır uygulanmaz; yalnızca sorun ayıklama içindir.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Sınırların uygulandığı zaman penceresi.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Bir agent'ın pencere başına gönderebileceği kayıt sayısı. Trafiği yoğun bir IIS
    /// sunucusu saniyede onlarca satır gönderebildiğinden cömert tutulur.
    /// </summary>
    [Range(MinPermitLimit, MaxPermitLimit)]
    public int IngestPermitLimit { get; set; } = 3_000;

    /// <summary>Bir panel oturumunun pencere başına yapabileceği sorgu sayısı.</summary>
    [Range(MinPermitLimit, MaxPermitLimit)]
    public int PanelPermitLimit { get; set; } = 600;

    /// <summary>
    /// Bir IP adresinin pencere başına deneyebileceği giriş sayısı. Hesap kilidinden
    /// bağımsız olarak parola denemesinin maliyetini yükseltir.
    /// </summary>
    [Range(MinPermitLimit, MaxPermitLimit)]
    public int LoginPermitLimit { get; set; } = 10;

    /// <summary>
    /// Sınır aşıldığında beklemeye alınacak istek sayısı. Sıfır: fazlası anında 429 alır.
    /// Agent'lar zaten yeniden deneme yaptığından kuyruk tutmaya gerek yoktur.
    /// </summary>
    [Range(MinQueueLimit, MaxQueueLimit)]
    public int QueueLimit { get; set; }
}
