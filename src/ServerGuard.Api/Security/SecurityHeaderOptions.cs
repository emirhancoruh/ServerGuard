namespace ServerGuard.Api.Security;

/// <summary>
/// Tarayıcıya gönderilen güvenlik header'ları.
/// </summary>
/// <remarks>
/// Değerler yapılandırılabilir tutulur: panel ileride farklı bir kaynaktan servis edilirse
/// veya harici bir kaynak eklenirse kod değiştirmeden ayarlanabilsin diye.
/// </remarks>
public sealed class SecurityHeaderOptions
{
    public const string SectionName = "Security:Headers";

    /// <summary>
    /// Varsayılan politika, paneli aynı kaynaktan (API'nin wwwroot'u) servis eden kuruluma
    /// göre yazılmıştır. Angular ve DevExtreme çalışma zamanında stil enjekte ettiğinden
    /// <c>style-src</c> satır içi stile izin verir; script için böyle bir izin verilmez.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    /// <summary>HSTS süresi. Yalnızca <c>Security:RequireHttps</c> açıkken gönderilir.</summary>
    public TimeSpan HstsMaxAge { get; set; } = TimeSpan.FromDays(365);
}
