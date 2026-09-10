using Microsoft.EntityFrameworkCore;
using ServerGuard.Api.Hosting;
using ServerGuard.Api.Repositories;

namespace ServerGuard.Api.Data;

public static class PersistenceExtensions
{
    private const string ConnectionStringName = "ServerGuard";
    private const int MaxRetryCount = 5;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"'{ConnectionStringName}' connection string tanımlı değil. " +
                "Geliştirmede 'dotnet user-secrets', production'da environment variable kullanın.");
        }

        services.AddDbContext<ServerGuardDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(MaxRetryCount, MaxRetryDelay, errorNumbersToAdd: null)));

        services.AddScoped<IServerMetricRepository, ServerMetricRepository>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<ISecurityAlertRepository, SecurityAlertRepository>();
        services.AddScoped<ITrafficLogRepository, TrafficLogRepository>();
        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();

        // Yalnizca hazir olma ucu veritabanina dokunur; canlilik ucu her cagrida
        // baglanti denemesi yapmaz.
        services
            .AddHealthChecks()
            .AddDbContextCheck<ServerGuardDbContext>(tags: [HealthEndpointExtensions.ReadinessTag]);

        return services;
    }
}
