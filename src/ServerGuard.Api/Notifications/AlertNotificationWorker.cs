using Microsoft.Extensions.Options;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Notifications;

/// <summary>
/// Kuyruğa bırakılan alarmları bildirim kanallarına iletir.
/// </summary>
/// <remarks>
/// Bu servis, alarmın kaydedilmesinden <b>tamamen ayrı</b> bir akışta çalışır. Telegram yavaş
/// olsa, zaman aşımına uğrasa veya hiç yanıt vermese bile alarmı kaydeden HTTP isteği bundan
/// etkilenmez; kayıt geri alınmaz. Bildirim hatası yalnızca loglanır.
/// </remarks>
public sealed class AlertNotificationWorker(
    ChannelAlertEventPublisher publisher,
    IEnumerable<IAlertNotifier> notifiers,
    IOptions<TelegramOptions> options,
    ILogger<AlertNotificationWorker> logger) : BackgroundService
{
    private readonly IReadOnlyList<IAlertNotifier> _notifiers = notifiers.ToList();
    private readonly TelegramOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Alert notification worker started. MinimumSeverity={MinimumSeverity} Notifiers={NotifierCount}",
            _options.MinimumSeverity,
            _notifiers.Count(notifier => notifier.IsEnabled));

        try
        {
            await foreach (var alert in publisher.Reader.ReadAllAsync(stoppingToken))
            {
                await NotifyAllAsync(alert, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }

        logger.LogInformation("Alert notification worker stopped.");
    }

    private async Task NotifyAllAsync(SecurityAlertDto alert, CancellationToken cancellationToken)
    {
        if (alert.Severity < _options.MinimumSeverity)
        {
            return;
        }

        foreach (var notifier in _notifiers)
        {
            if (!notifier.IsEnabled)
            {
                continue;
            }

            try
            {
                await notifier.NotifyAsync(alert, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Bir kanalın hatası diğerlerini ve döngüyü durdurmamalı.
                logger.LogError(
                    exception,
                    "Alert notification failed. Notifier={Notifier} AlertId={AlertId}",
                    notifier.GetType().Name,
                    alert.Id);
            }
        }
    }
}
