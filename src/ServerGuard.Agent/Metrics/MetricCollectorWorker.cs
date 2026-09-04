using Microsoft.Extensions.Options;
using ServerGuard.Agent.Configuration;
using ServerGuard.Agent.Transport;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Agent.Metrics;

/// <summary>
/// Belirli aralıklarla CPU/RAM okur, kuyruğa ekler ve kuyruğu backend'e boşaltır.
/// Backend erişilemezse kayıtlar kuyrukta bekler; bir sonraki tick'te tekrar denenir.
/// </summary>
public sealed class MetricCollectorWorker(
    ISystemMetricsReader metricsReader,
    BackendDispatcher<ServerMetricDto> dispatcher,
    IOptions<AgentOptions> agentOptions,
    IOptions<MetricsOptions> metricsOptions,
    TimeProvider timeProvider,
    ILogger<MetricCollectorWorker> logger) : BackgroundService
{
    private readonly AgentOptions _agentOptions = agentOptions.Value;
    private readonly MetricsOptions _options = metricsOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Metric collection is disabled by configuration.");
            return;
        }

        logger.LogInformation(
            "Metric collector started. Server={ServerName} Interval={Interval} Backend={Backend}",
            _agentOptions.ServerName,
            _options.CollectionInterval,
            _agentOptions.ApiBaseUrl);

        using var timer = new PeriodicTimer(_options.CollectionInterval, timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CollectAndFlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }

        logger.LogInformation("Metric collector stopped. PendingInQueue={Count}", dispatcher.PendingCount);
    }

    private async Task CollectAndFlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = metricsReader.Read();

            dispatcher.Enqueue(new ServerMetricDto(
                _agentOptions.ServerName,
                snapshot.CpuUsagePercent,
                snapshot.RamUsagePercent,
                timeProvider.GetUtcNow()));

            await dispatcher.FlushAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Tek bir döngü hatası servisi düşürmemeli; bir sonraki tick'te devam edilir.
            logger.LogError(exception, "Metric collection cycle failed; will retry on next tick.");
        }
    }
}
