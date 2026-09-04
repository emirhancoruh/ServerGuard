using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Belirli bir süredir istek görülmeyen IP sayaçlarını periyodik olarak siler.
/// Bu tarama olmadan sözlük, uygulama çalıştıkça gördüğü her IP'yi kalıcı olarak tutar
/// ve bellek sınırsız büyür.
/// </summary>
public sealed class TrafficWindowCleanupService(
    ITrafficWindowStore store,
    IOptions<TrafficAnomalyOptions> options,
    TimeProvider timeProvider,
    ILogger<TrafficWindowCleanupService> logger) : BackgroundService
{
    private readonly TrafficAnomalyOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        logger.LogInformation(
            "Traffic counter cleanup started. Interval={Interval} IdleRetention={IdleRetention}",
            _options.CleanupInterval,
            _options.IdleRetention);

        using var timer = new PeriodicTimer(_options.CleanupInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Sweep();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }
    }

    private void Sweep()
    {
        try
        {
            var removed = store.RemoveIdle(timeProvider.GetUtcNow(), _options.IdleRetention);

            if (removed > 0)
            {
                logger.LogInformation(
                    "Idle traffic counters removed. Removed={Removed} Remaining={Remaining}",
                    removed,
                    store.TrackedCount);
            }
        }
        catch (Exception exception)
        {
            // Tek bir taramanın hatası servisi düşürmemeli.
            logger.LogError(exception, "Traffic counter cleanup failed; will retry on next interval.");
        }
    }
}
