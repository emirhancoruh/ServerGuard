using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ServerGuard.Shared;
using ServerGuard.Shared.Security;

namespace ServerGuard.Api.Security;

/// <summary>
/// Kimlik doğrulama ve yetkilendirmenin tek kayıt noktası.
/// </summary>
public static class SecurityExtensions
{
    /// <summary>Token süresi kontrolünde sunucular arası saat farkı için tanınan pay.</summary>
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddApiSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .ValidateOnStart();

        // Geliştirmede imza anahtarı verilmemişse süreç ömrü boyunca geçerli bir anahtar üretilir.
        // Production'da bu yapılmaz: anahtarın her açılışta değişmesi tüm oturumları düşürür ve
        // eksik yapılandırmayı gizler. Orada açılış SecurityOptionsValidator ile durdurulur.
        if (!environment.IsProduction())
        {
            services.PostConfigure<SecurityOptions>(options =>
            {
                if (string.IsNullOrWhiteSpace(options.Jwt.SigningKey))
                {
                    options.Jwt.SigningKey = SecretGenerator.Create();
                }
            });
        }

        services.AddSingleton<IValidateOptions<SecurityOptions>>(
            _ => new SecurityOptionsValidator(environment));

        services.AddSingleton<IIngestKeyValidator, IngestKeyValidator>();
        services.AddSingleton<LoginAttemptTracker>();
        services.AddSingleton<IPanelAuthenticator, PanelAuthenticator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddHostedService<SecurityConfigurationAudit>();

        services.AddAuthentication(AuthenticationDefaults.PanelScheme)
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                AuthenticationDefaults.ApiKeyScheme,
                configureOptions: null)
            .AddJwtBearer(AuthenticationDefaults.PanelScheme, _ => { });

        // JWT ayarları SecurityOptions'a bağımlı olduğundan burada değil, DI çözümlenirken
        // yapılandırılır. Aksi halde imza anahtarını okumak için kayıt sırasında ikinci bir
        // servis sağlayıcı kurmak gerekirdi.
        services
            .AddOptions<JwtBearerOptions>(AuthenticationDefaults.PanelScheme)
            .Configure<IOptions<SecurityOptions>>(
                (jwtOptions, securityOptions) => ConfigurePanelScheme(jwtOptions, securityOptions.Value));

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Ingest, policy => policy
                .AddAuthenticationSchemes(AuthenticationDefaults.ApiKeyScheme)
                .RequireAuthenticatedUser())
            .AddPolicy(AuthorizationPolicies.Panel, policy => policy
                .AddAuthenticationSchemes(AuthenticationDefaults.PanelScheme)
                .RequireAuthenticatedUser());

        return services;
    }

    private static void ConfigurePanelScheme(JwtBearerOptions options, SecurityOptions security)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = security.Jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = security.Jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKeyFactory.Create(security.Jwt.SigningKey),
            ValidateLifetime = true,
            ClockSkew = ClockSkew,
            NameClaimType = JwtRegisteredClaimNames.Sub
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Tarayıcı WebSocket el sıkışmasında özel header gönderemez; SignalR token'ı
                // sorgu parametresiyle taşır. Bu kabul yalnızca hub yolu için geçerlidir,
                // aksi halde token'lar erişim log'larına ve tarayıcı geçmişine sızardı.
                if (!IsHubRequest(context.HttpContext.Request.Path))
                {
                    return Task.CompletedTask;
                }

                var token = context.Request.Query[AuthConstraints.AccessTokenQueryParameter];

                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    }

    private static bool IsHubRequest(PathString path) =>
        path.StartsWithSegments(ApiRoutes.MonitoringHub, StringComparison.OrdinalIgnoreCase);
}
