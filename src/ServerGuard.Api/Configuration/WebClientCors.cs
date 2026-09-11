namespace ServerGuard.Api.Configuration;

/// <summary>
/// Panelin tarayıcıdan API'ye ve SignalR hub'ına erişebilmesi için CORS politikası.
/// İzinli origin'ler "Cors:AllowedOrigins" bölümünden okunur.
/// </summary>
/// <remarks>
/// Panel ayrı bir sitede yayınlandığı için bu politika <b>zorunludur</b>: listede panelin
/// origin'i yoksa tarayıcı her isteği engeller ve panel boş görünür. Origin, şema + sunucu
/// adı + port üçlüsüdür ve panelin adres çubuğundaki hâliyle birebir eşleşmelidir —
/// <c>http://sunucu10:8090</c> ile <c>http://10.0.0.10:8090</c> farklı origin'lerdir.
/// </remarks>
public static class WebClientCors
{
    public const string PolicyName = "WebClient";
    public const string AllowedOriginsKey = "Cors:AllowedOrigins";

    private const string LoopbackHost = "localhost";

    public static IServiceCollection AddWebClientCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var allowedOrigins = ResolveOrigins(configuration, environment);

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }

    /// <summary>
    /// Yapılandırmadaki origin listesini, ortama göre süzerek döndürür.
    /// </summary>
    /// <remarks>
    /// Geliştirme adresleri production'a sızarsa, geliştirici makinesinde açılmış bir sayfa
    /// canlı verilere erişebilir hale gelir; bu yüzden orada loopback adresleri elenir.
    /// Aynı metot hem politikayı kurarken hem de açılışta log'a yazarken kullanılır, böylece
    /// log'da görünen liste ile gerçekten uygulanan liste ayrışamaz.
    /// </remarks>
    public static string[] ResolveOrigins(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];

        return environment.IsProduction()
            ? [.. configured.Where(origin => !IsLoopback(origin))]
            : configured;
    }

    private static bool IsLoopback(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        (uri.IsLoopback || string.Equals(uri.Host, LoopbackHost, StringComparison.OrdinalIgnoreCase));
}
