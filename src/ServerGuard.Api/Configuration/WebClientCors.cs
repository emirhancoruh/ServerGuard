namespace ServerGuard.Api.Configuration;

/// <summary>
/// Web istemcisinin (Angular) tarayıcıdan API ve SignalR hub'ına erişebilmesi için CORS politikası.
/// İzinli origin'ler "Cors:AllowedOrigins" bölümünden okunur.
/// </summary>
public static class WebClientCors
{
    public const string PolicyName = "WebClient";
    private const string AllowedOriginsKey = "Cors:AllowedOrigins";

    public static IServiceCollection AddWebClientCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection(AllowedOriginsKey).Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

        return services;
    }
}
