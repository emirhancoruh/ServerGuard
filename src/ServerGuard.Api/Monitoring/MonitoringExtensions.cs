namespace ServerGuard.Api.Monitoring;

public static class MonitoringExtensions
{
    public static IServiceCollection AddMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<ServerHealthOptions>()
            .Bind(configuration.GetSection(ServerHealthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ServerHealthEvaluator>();
        services.AddScoped<IMonitoringOverviewService, MonitoringOverviewService>();

        return services;
    }
}
