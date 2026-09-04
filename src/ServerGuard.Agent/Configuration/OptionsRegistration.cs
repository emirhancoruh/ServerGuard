namespace ServerGuard.Agent.Configuration;

public static class OptionsRegistration
{
    /// <summary>
    /// Bir ayar bölümünü bağlar ve DataAnnotations kurallarını uygulama açılışında doğrular.
    /// Hatalı konfigürasyon ilk kullanımda değil, hemen fark edilir.
    /// </summary>
    public static IServiceCollection AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
