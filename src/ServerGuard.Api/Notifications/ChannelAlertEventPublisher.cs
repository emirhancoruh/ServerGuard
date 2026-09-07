using System.Threading.Channels;
using Microsoft.Extensions.Options;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Notifications;

/// <summary>
/// Alarmları sınırlı kapasiteli bir kanala yazar; <see cref="AlertNotificationWorker"/> okur.
/// Kapasite dolarsa en eski bildirim atılır ve bu loglanır — bildirim sessizce kaybolmaz,
/// kuyruk da sınırsız büyümez.
/// </summary>
public sealed class ChannelAlertEventPublisher : IAlertEventPublisher
{
    private readonly Channel<SecurityAlertDto> _channel;
    private readonly int _capacity;
    private readonly ILogger<ChannelAlertEventPublisher> _logger;

    public ChannelAlertEventPublisher(
        IOptions<TelegramOptions> options,
        ILogger<ChannelAlertEventPublisher> logger)
    {
        _logger = logger;
        _capacity = options.Value.QueueCapacity;

        _channel = Channel.CreateBounded<SecurityAlertDto>(
            new BoundedChannelOptions(_capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            },
            OnDropped);
    }

    /// <summary>Arka plan servisinin okuduğu uç.</summary>
    public ChannelReader<SecurityAlertDto> Reader => _channel.Reader;

    public void Publish(SecurityAlertDto alert) => _channel.Writer.TryWrite(alert);

    private void OnDropped(SecurityAlertDto dropped) =>
        _logger.LogWarning(
            "Notification queue full (Capacity={Capacity}); oldest alert notification dropped. " +
            "AlertId={AlertId} Server={ServerName}",
            _capacity,
            dropped.Id,
            dropped.ServerName);
}
