using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Api.Detection;

/// <summary>
/// appsettings.json "Detection:TrafficAnomaly" bölümünden bağlanan tespit kuralı ayarları.
/// Hiçbir eşik koda gömülü değildir.
/// </summary>
public sealed class TrafficAnomalyOptions
{
    public const string SectionName = "Detection:TrafficAnomaly";

    private const int MinRequestThreshold = 2;
    private const int MaxRequestThreshold = 1_000_000;
    private const string MinWindow = "00:00:05";
    private const string MaxWindow = "01:00:00";
    private const string MinCooldown = "00:00:00";
    private const string MaxCooldown = "24:00:00";
    private const string MinRetention = "00:01:00";
    private const string MaxRetention = "24:00:00";
    private const string MinCleanupInterval = "00:00:10";
    private const string MaxCleanupInterval = "01:00:00";
    private const int MinBucketCount = 2;
    private const int MaxBucketCount = 240;
    private const long MinTrackedIpLimit = 100;
    private const long MaxTrackedIpLimit = 5_000_000;

    public bool Enabled { get; set; } = true;

    /// <summary>Pencere içinde bu sayının <b>üzerine</b> çıkılırsa alarm üretilir.</summary>
    [Range(MinRequestThreshold, MaxRequestThreshold)]
    public int RequestThreshold { get; set; } = 100;

    /// <summary>İsteklerin sayıldığı zaman aralığı.</summary>
    [Range(typeof(TimeSpan), MinWindow, MaxWindow)]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Bir IP için alarm üretildikten sonra aynı IP için yeni alarm üretilmeden geçmesi gereken süre.
    /// Süren bir tarama sırasında saniyede onlarca alarm üretilmesini engeller.
    /// </summary>
    [Range(typeof(TimeSpan), MinCooldown, MaxCooldown)]
    public TimeSpan AlertCooldown { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Pencerenin kaç dilime bölüneceği. Dilim sayısı arttıkça sayım hassaslaşır,
    /// IP başına bellek de artar. 6 dilim, 1 dakikalık pencerede 10 saniyelik çözünürlük verir.
    /// </summary>
    [Range(MinBucketCount, MaxBucketCount)]
    public int BucketCount { get; set; } = 6;

    /// <summary>Bu süredir istek görülmeyen IP'nin sayacı bellekten silinir.</summary>
    [Range(typeof(TimeSpan), MinRetention, MaxRetention)]
    public TimeSpan IdleRetention { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Temizlik taramasının çalışma sıklığı.</summary>
    [Range(typeof(TimeSpan), MinCleanupInterval, MaxCleanupInterval)]
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Aynı anda izlenebilecek en fazla IP sayısı. Temizlik taramaları arasında sahte IP'lerle
    /// yapılan bir selin belleği doldurmasını engelleyen sert üst sınırdır.
    /// </summary>
    [Range(MinTrackedIpLimit, MaxTrackedIpLimit)]
    public long TrackedIpLimit { get; set; } = 100_000;
}
