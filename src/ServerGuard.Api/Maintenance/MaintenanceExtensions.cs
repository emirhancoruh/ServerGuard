namespace ServerGuard.Api.Maintenance;

public static class MaintenanceExtensions
{
    public static IServiceCollection AddMaintenance(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<RetentionOptions>()
            .Bind(configuration.GetSection(RetentionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IDataRetentionCleaner, DataRetentionCleaner>();
        services.AddHostedService<DataRetentionService>();

        return services;
    }
}
