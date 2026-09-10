namespace ServerGuard.Api.Configuration;

/// <summary>
/// Web istemcisinin (Angular) tarayıcıdan API ve SignalR hub'ına erişebilmesi için CORS politikası.
/// İzinli origin'ler "Cors:AllowedOrigins" bölümünden okunur.
/// </summary>
/// <remarks>
/// Panel API ile aynı kaynaktan servis edildiğinde bu politikaya hiç ihtiyaç olmaz; liste boş
/// bırakılabilir. Politika yalnızca <c>ng serve</c> ile geliştirme veya paneli ayrı bir sitede
/// yayınlama durumları için vardır.
/// </remarks>
public static class WebClientCors
{
    public const string PolicyName = "WebClient";
    private const string AllowedOriginsKey = "Cors:AllowedOrigins";
    private const string LoopbackHost = "localhost";

    public static IServiceCollection AddWebClientCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];

        // Geliştirme adresleri production'a sızarsa, geliştirici makinesinde açılmış bir sayfa
        // canlı verilere erişebilir hale gelir. Bu yüzden orada sessizce elenir.
        if (environment.IsProduction())
        {
            allowedOrigins = [.. allowedOrigins.Where(origin => !IsLoopback(origin))];
        }

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }

    private static bool IsLoopback(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        (uri.IsLoopback || string.Equals(uri.Host, LoopbackHost, StringComparison.OrdinalIgnoreCase));
}
