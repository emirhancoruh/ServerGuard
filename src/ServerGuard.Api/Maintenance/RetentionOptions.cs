using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Api.Maintenance;

/// <summary>
/// Veri saklama süreleri. Süresi dolan kayıtlar düzenli aralıklarla silinir.
/// </summary>
/// <remarks>
/// Bu ayar olmadan tablolar sınırsız büyür; disk dolduğunda SQL Server durur ve izleme
/// sisteminin kendisi çöker. Süreler tablo bazında ayrı tutulur çünkü değerleri farklıdır:
/// metrik verisi hızla değerini yitirir, güvenlik alarmları ise adli inceleme için uzun süre gerekir.
/// </remarks>
public sealed class RetentionOptions
{
    public const string SectionName = "Maintenance:Retention";

    private const string MinRetention = "1.00:00:00";
    private const string MaxRetention = "3650.00:00:00";
    private const string MinInterval = "00:05:00";
    private const string MaxInterval = "7.00:00:00";
    private const string MinDelay = "00:00:00";
    private const string MaxDelay = "01:00:00";
    private const int MinBatchSize = 100;
    private const int MaxBatchSize = 100_000;

    /// <summary>Kapatıldığında hiçbir kayıt silinmez; tabloların büyümesi izlenmelidir.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Temizliğin ne sıklıkla çalışacağı.</summary>
    [Range(typeof(TimeSpan), MinInterval, MaxInterval)]
    public TimeSpan RunInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Açılıştan sonra ilk çalıştırmaya kadar beklenen süre. Uygulama açılırken
    /// veritabanına ek yük binmesin diye vardır.
    /// </summary>
    [Range(typeof(TimeSpan), MinDelay, MaxDelay)]
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Tek bir DELETE ifadesinde silinecek en fazla satır. Küçük partiler hâlinde silmek,
    /// uzun süren tek bir işlemin tabloyu kilitlemesini ve log dosyasını şişirmesini önler.
    /// </summary>
    [Range(MinBatchSize, MaxBatchSize)]
    public int BatchSize { get; set; } = 5_000;

    [Range(typeof(TimeSpan), MinRetention, MaxRetention)]
    public TimeSpan ServerMetrics { get; set; } = TimeSpan.FromDays(30);

    [Range(typeof(TimeSpan), MinRetention, MaxRetention)]
    public TimeSpan TrafficLogs { get; set; } = TimeSpan.FromDays(30);

    [Range(typeof(TimeSpan), MinRetention, MaxRetention)]
    public TimeSpan SecurityEvents { get; set; } = TimeSpan.FromDays(90);

    [Range(typeof(TimeSpan), MinRetention, MaxRetention)]
    public TimeSpan SecurityAlerts { get; set; } = TimeSpan.FromDays(365);
}
