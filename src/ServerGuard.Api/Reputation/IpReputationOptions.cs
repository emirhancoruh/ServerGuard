using System.ComponentModel.DataAnnotations;

namespace ServerGuard.Api.Reputation;

/// <summary>
/// AbuseIPDB itibar sorgusunun ayarları.
/// </summary>
/// <remarks>
/// <b>ApiKey buraya YAZILMAZ.</b> Geliştirmede <c>dotnet user-secrets</c>, production'da
/// <c>Detection__IpReputation__ApiKey</c> ortam değişkeni ile verilir. Anahtar tanımlı değilse
/// servis sessizce devre dışı kalır; sistem itibar bilgisi olmadan çalışmayı sürdürür.
/// </remarks>
public sealed class IpReputationOptions
{
    public const string SectionName = "Detection:IpReputation";

    private const string MinCacheDuration = "00:01:00";
    private const string MaxCacheDuration = "24:00:00";
    private const int MinMaxAgeInDays = 1;
    private const int MaxMaxAgeInDays = 365;
    private const long MinCachedIpLimit = 100;
    private const long MaxCachedIpLimit = 1_000_000;

    public bool Enabled { get; set; } = true;

    /// <summary>Yalnızca sır deposundan okunur; appsettings.json'da boş kalır.</summary>
    public string? ApiKey { get; set; }

    public Uri BaseAddress { get; set; } = new("https://api.abuseipdb.com/api/v2/");

    /// <summary>AbuseIPDB'nin kaç günlük rapor geçmişini dikkate alacağı.</summary>
    [Range(MinMaxAgeInDays, MaxMaxAgeInDays)]
    public int MaxAgeInDays { get; set; } = 90;

    /// <summary>
    /// Aynı adresin tekrar sorgulanmayacağı süre. Ücretsiz tier'ın günlük kotasına
    /// takılmamak için zorunludur.
    /// </summary>
    [Range(typeof(TimeSpan), MinCacheDuration, MaxCacheDuration)]
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Önbellekte aynı anda tutulabilecek en fazla adres.</summary>
    [Range(MinCachedIpLimit, MaxCachedIpLimit)]
    public long CachedIpLimit { get; set; } = 10_000;
}
