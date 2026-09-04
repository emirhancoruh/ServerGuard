using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Api.Detection;

/// <summary>
/// appsettings.json "Detection:BruteForce" bölümünden bağlanan tespit kuralı ayarları.
/// </summary>
public sealed class BruteForceOptions
{
    public const string SectionName = "Detection:BruteForce";

    private const int MinThreshold = 2;
    private const int MaxThreshold = 1_000;
    private const string MinWindow = "00:00:10";
    private const string MaxWindow = "01:00:00";
    private const long MinTrackedIpLimit = 100;
    private const long MaxTrackedIpLimit = 5_000_000;

    public bool Enabled { get; set; } = true;

    /// <summary>Pencere içinde alarm üretmek için gereken başarısız giriş sayısı.</summary>
    [Range(MinThreshold, MaxThreshold)]
    public int FailureThreshold { get; set; } = 5;

    /// <summary>Başarısız girişlerin sayıldığı zaman aralığı.</summary>
    [Range(typeof(TimeSpan), MinWindow, MaxWindow)]
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Aynı anda izlenebilecek en fazla IP sayısı. Sahte IP'lerle yapılan bir sel saldırısında
    /// belleğin sınırsız büyümesini engeller; sınır aşılınca en az kullanılan pencereler düşer.
    /// </summary>
    [Range(MinTrackedIpLimit, MaxTrackedIpLimit)]
    public long TrackedIpLimit { get; set; } = 50_000;
}
