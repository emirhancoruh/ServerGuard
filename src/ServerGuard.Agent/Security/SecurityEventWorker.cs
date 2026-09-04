using System.Diagnostics.Eventing.Reader;
using Microsoft.Extensions.Options;
using ServerGuard.Agent.Configuration;
using ServerGuard.Agent.Transport;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Agent.Security;

/// <summary>
/// Windows Security kanalını dinler, başarılı/başarısız oturum açma olaylarını yakalar
/// ve backend'e gönderir. Olay işleyicisi bloklamaz: olay ayrıştırılıp kuyruğa bırakılır,
/// gönderim ayrı bir döngüde yapılır.
/// </summary>
public sealed class SecurityEventWorker(
    SecurityEventParser parser,
    BackendDispatcher<SecurityEventDto> dispatcher,
    IOptions<AgentOptions> agentOptions,
    IOptions<SecurityEventOptions> securityEventOptions,
    TimeProvider timeProvider,
    ILogger<SecurityEventWorker> logger) : BackgroundService
{
    private const string SecurityLogName = "Security";

    private const string PermissionHint =
        "Windows, Security kanalını okumayı reddetti. Agent'ın çalıştığı hesabı yerel " +
        "'Event Log Readers' grubuna ekleyin veya Agent:SecurityEvents:Enabled ayarını false yapın.";

    private readonly AgentOptions _agentOptions = agentOptions.Value;
    private readonly SecurityEventOptions _options = securityEventOptions.Value;

    private EventLogWatcher? _watcher;

    /// <summary>Abonelik kalıcı olarak başarısız olduğunda bekleme döngüsünü sonlandırır.</summary>
    private CancellationTokenSource? _watcherFailed;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Security event collection is disabled by configuration.");
            return;
        }

        using var watcherFailed = new CancellationTokenSource();
        _watcherFailed = watcherFailed;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, watcherFailed.Token);

        if (!TryStartWatcher())
        {
            return;
        }

        logger.LogInformation("Security event watcher started. Server={ServerName}", _agentOptions.ServerName);

        try
        {
            using var timer = new PeriodicTimer(_options.FlushInterval, timeProvider);

            while (await timer.WaitForNextTickAsync(linked.Token))
            {
                // Kalan olayların gönderilebilmesi için abonelik hatası değil, yalnızca kapanış iptal eder.
                await dispatcher.FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Kapanış ya da abonelik hatası; her ikisinde de kuyruk son kez boşaltılır.
        }
        finally
        {
            StopWatcher();
            _watcherFailed = null;
        }

        await FlushRemainingAsync(stoppingToken);

        logger.LogInformation("Security event watcher stopped. PendingInQueue={Count}", dispatcher.PendingCount);
    }

    public override void Dispose()
    {
        StopWatcher();
        base.Dispose();
    }

    private async Task FlushRemainingAsync(CancellationToken stoppingToken)
    {
        if (stoppingToken.IsCancellationRequested || dispatcher.PendingCount == 0)
        {
            return;
        }

        await dispatcher.FlushAsync(stoppingToken);
    }

    private bool TryStartWatcher()
    {
        try
        {
            var query = new EventLogQuery(SecurityLogName, PathType.LogName, WindowsSecurityEventIds.LogonEventsXPath);

            _watcher = new EventLogWatcher(query);
            _watcher.EventRecordWritten += OnEventRecordWritten;
            _watcher.Enabled = true;

            return true;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or EventLogException)
        {
            // Yetki yoksa yalnızca bu toplayıcı devre dışı kalır; agent'ın geri kalanı çalışmayı sürdürür.
            logger.LogError("{Hint} Detay: {Reason}: {Message}", PermissionHint, exception.GetType().Name, exception.Message);

            StopWatcher();
            return false;
        }
    }

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EventRecordWritten -= OnEventRecordWritten;
        _watcher.Enabled = false;
        _watcher.Dispose();
        _watcher = null;
    }

    /// <summary>
    /// Watcher'ın kendi thread'inde çağrılır. Bloklamamalı ve exception sızdırmamalıdır;
    /// aksi halde olay dinleme tamamen durur.
    /// </summary>
    private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs args)
    {
        try
        {
            if (args.EventException is not null)
            {
                // Windows bu noktadan sonra olay göndermeyi bırakır; toplayıcıyı sessizce
                // çalışıyormuş gibi göstermek yerine açıkça durduruyoruz.
                logger.LogError(
                    "{Hint} Abonelik durduruldu. Detay: {Reason}: {Message}",
                    PermissionHint,
                    args.EventException.GetType().Name,
                    args.EventException.Message);

                _watcherFailed?.Cancel();
                return;
            }

            using var record = args.EventRecord;

            if (record is null)
            {
                logger.LogWarning("Security event skipped: empty record received.");
                return;
            }

            if (parser.TryParse(record.ToXml(), _agentOptions.ServerName, timeProvider.GetUtcNow(), out var securityEvent))
            {
                dispatcher.Enqueue(securityEvent);
            }
        }
        catch (Exception exception)
        {
            // Tek bir olayın hatası dinlemeyi durdurmamalı.
            logger.LogWarning(
                "Security event could not be processed ({Reason}): {Message}",
                exception.GetType().Name,
                exception.Message);
        }
    }
}
