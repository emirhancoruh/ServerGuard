namespace ServerGuard.Api.Detection;

public static class DetectionExtensions
{
    public static IServiceCollection AddDetection(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<BruteForceOptions>()
            .Bind(configuration.GetSection(BruteForceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<TrafficAnomalyOptions>()
            .Bind(configuration.GetSection(TrafficAnomalyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Sayaçlar istekler arasında yaşamalı; store'lar singleton'dır.
        services.AddSingleton<IFailureWindowStore, MemoryCacheFailureWindowStore>();
        services.AddSingleton<ITrafficWindowStore, TrafficWindowStore>();

        services.AddScoped<IBruteForceDetectionService, BruteForceDetectionService>();
        services.AddScoped<ITrafficAnomalyDetectionService, TrafficAnomalyDetectionService>();

        services.AddHostedService<TrafficWindowCleanupService>();

        return services;
    }
}
