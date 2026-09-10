using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Security;

/// <summary>
/// Her yanıta tarayıcı tarafı savunma header'larını ekler.
/// </summary>
/// <remarks>
/// Header'lar yanıt gövdesi yazılmadan önce, boru hattının en başında eklenir; böylece
/// controller yanıtları, statik dosyalar ve hata yanıtları dahil her yanıtı kapsar.
/// </remarks>
public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IOptions<SecurityHeaderOptions> headerOptions,
    IOptions<SecurityOptions> securityOptions)
{
    private const string ContentTypeOptionsHeader = "X-Content-Type-Options";
    private const string FrameOptionsHeader = "X-Frame-Options";
    private const string ReferrerPolicyHeader = "Referrer-Policy";
    private const string PermissionsPolicyHeader = "Permissions-Policy";
    private const string ContentSecurityPolicyHeader = "Content-Security-Policy";
    private const string StrictTransportSecurityHeader = "Strict-Transport-Security";

    private const string NoSniff = "nosniff";
    private const string DenyFraming = "DENY";
    private const string NoReferrer = "no-referrer";
    private const string DisabledFeatures = "geolocation=(), camera=(), microphone=(), payment=()";

    private readonly SecurityHeaderOptions _headers = headerOptions.Value;
    private readonly bool _requireHttps = securityOptions.Value.RequireHttps;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers[ContentTypeOptionsHeader] = NoSniff;
        headers[FrameOptionsHeader] = DenyFraming;
        headers[ReferrerPolicyHeader] = NoReferrer;
        headers[PermissionsPolicyHeader] = DisabledFeatures;

        if (!string.IsNullOrWhiteSpace(_headers.ContentSecurityPolicy))
        {
            headers[ContentSecurityPolicyHeader] = _headers.ContentSecurityPolicy;
        }

        // HSTS yalnızca HTTPS zorunluyken anlamlıdır: tarayıcıya "bu adrese bir daha asla
        // HTTP ile gitme" der. Sertifika hazır değilken gönderilirse panel erişilemez hale gelir.
        if (_requireHttps)
        {
            headers[StrictTransportSecurityHeader] =
                $"max-age={(int)_headers.HstsMaxAge.TotalSeconds}; includeSubDomains";
        }

        return next(context);
    }
}
