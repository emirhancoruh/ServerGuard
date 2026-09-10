namespace ServerGuard.Api.Security;

/// <summary>
/// API'nin tüm kimlik doğrulama ve taşıma güvenliği ayarları.
/// </summary>
/// <remarks>
/// Bu bölümdeki hiçbir sır kaynak koda veya <c>appsettings.json</c>'a yazılmaz.
/// Geliştirmede <c>dotnet user-secrets</c>, production'da ortam değişkenleri kullanılır
/// (<c>Security__Jwt__SigningKey</c> gibi). Doğrulama <see cref="SecurityOptionsValidator"/>
/// tarafından uygulama açılışında yapılır.
/// </remarks>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// HTTP isteklerinin HTTPS'e yönlendirilip yönlendirilmeyeceği ve HSTS'in açılıp açılmayacağı.
    /// Agent'lar güvenilmeyen bir sertifikaya bağlanamayacağından, geçerli sertifika kurulmadan
    /// açılmamalıdır.
    /// </summary>
    public bool RequireHttps { get; set; }

    public JwtOptions Jwt { get; set; } = new();

    public PanelOptions Panel { get; set; } = new();

    public IngestOptions Ingest { get; set; } = new();
}

/// <summary>Panel oturum token'ının üretim ve doğrulama ayarları.</summary>
public sealed class JwtOptions
{
    /// <summary>HS256 için gereken en kısa anahtar uzunluğu (256 bit).</summary>
    public const int MinimumSigningKeyLength = 32;

    public string Issuer { get; set; } = "ServerGuard";

    public string Audience { get; set; } = "ServerGuard.Panel";

    /// <summary>Base64 veya düz metin; en az <see cref="MinimumSigningKeyLength"/> karakter.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(8);
}

/// <summary>Panele giriş yapabilecek kullanıcılar ve kaba kuvvete karşı kilitleme.</summary>
public sealed class PanelOptions
{
    public IReadOnlyList<PanelUserOptions> Users { get; set; } = [];

    /// <summary>Bu sayıda başarısız denemeden sonra kullanıcı geçici olarak kilitlenir.</summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>Kilit süresi. Süre dolduğunda sayaç sıfırlanır.</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Aynı anda izlenecek en fazla başarısız giriş kaydı; bellek sınırsız büyümez.</summary>
    public int TrackedAttemptLimit { get; set; } = 10_000;
}

/// <summary>Tek bir panel kullanıcısı. Parola yalnızca özet olarak tutulur.</summary>
public sealed class PanelUserOptions
{
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// <c>ServerGuard.Tools hash-password</c> ile üretilen PBKDF2 özeti.
    /// Düz parola buraya asla yazılmaz.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;
}

/// <summary>Agent'ların veri gönderirken kullandığı anahtarlar.</summary>
public sealed class IngestOptions
{
    public IReadOnlyList<IngestKeyOptions> ApiKeys { get; set; } = [];
}

/// <summary>
/// Tek bir agent anahtarı. Her sunucuya ayrı anahtar verilirse biri sızdığında
/// yalnızca o anahtar iptal edilir.
/// </summary>
public sealed class IngestKeyOptions
{
    /// <summary>Log'larda görünen tanımlayıcı ad (örn. sunucu adı). Anahtarın kendisi loglanmaz.</summary>
    public string Name { get; set; } = string.Empty;

    public string Key { get; set; } = string.Empty;
}
