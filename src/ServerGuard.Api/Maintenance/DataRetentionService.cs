using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Maintenance;

/// <summary>
/// Veri temizliğini düzenli aralıklarla çalıştırır.
/// </summary>
/// <remarks>
/// <see cref="BackgroundService"/> singleton olduğundan scoped olan
/// <see cref="IDataRetentionCleaner"/> her turda yeni bir scope içinde çözümlenir.
/// Bir turun hatası servisi düşürmez; bir sonraki turda yeniden denenir.
/// </remarks>
public sealed class DataRetentionService(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    ILogger<DataRetentionService> logger) : BackgroundService
{
    private readonly RetentionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogWarning(
                "Data retention is disabled ({SectionName}:Enabled=false); tables will grow without bound.",
                RetentionOptions.SectionName);

            return;
        }

        logger.LogInformation(
            "Data retention started. Interval={Interval} Metrics={Metrics} Traffic={Traffic} Events={Events} Alerts={Alerts}",
            _options.RunInterval,
            _options.ServerMetrics,
            _options.TrafficLogs,
            _options.SecurityEvents,
            _options.SecurityAlerts);

        try
        {
            await Task.Delay(_options.InitialDelay, stoppingToken);

            using var timer = new PeriodicTimer(_options.RunInterval);

            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }

        logger.LogInformation("Data retention stopped.");
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var cleaner = scope.ServiceProvider.GetRequiredService<IDataRetentionCleaner>();

            var report = await cleaner.CleanAsync(cancellationToken);

            logger.LogInformation(
                "Retention run finished. Total={Total} Metrics={Metrics} Traffic={Traffic} Events={Events} Alerts={Alerts}",
                report.Total,
                report.ServerMetrics,
                report.TrafficLogs,
                report.SecurityEvents,
                report.SecurityAlerts);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Retention run failed; will retry on the next interval.");
        }
    }
}
