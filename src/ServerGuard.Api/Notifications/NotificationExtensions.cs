namespace ServerGuard.Api.Notifications;

public static class NotificationExtensions
{
    public static IServiceCollection AddAlertNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddTelegramClient();

        // Kuyruk istekler arasında yaşamalı; yayıncı singleton'dır ve arka plan servisi
        // aynı örneğin okuma ucunu kullanır.
        services.AddSingleton<ChannelAlertEventPublisher>();
        services.AddSingleton<IAlertEventPublisher>(provider => provider.GetRequiredService<ChannelAlertEventPublisher>());

        // Yeni bir kanal (e-posta, webhook) eklemek için buraya bir kayıt yeterlidir;
        // arka plan servisi değişmez.
        services.AddSingleton<IAlertNotifier, TelegramNotificationService>();

        services.AddHostedService<AlertNotificationWorker>();

        return services;
    }
}
