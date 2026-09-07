namespace ServerGuard.Api.Reputation;

public static class ReputationExtensions
{
    public static IServiceCollection AddIpReputation(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<IpReputationOptions>()
            .Bind(configuration.GetSection(IpReputationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddAbuseIpDbClient();

        // Onbellek istekler arasinda yasamali; servis singleton'dir ve kendi cache'ini yonetir.
        services.AddSingleton<IIpReputationService, AbuseIpDbReputationService>();

        return services;
    }
}
