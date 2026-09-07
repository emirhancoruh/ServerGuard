using Microsoft.Extensions.Options;
using Polly;

namespace ServerGuard.Api.Notifications;

/// <summary>
/// Telegram Bot API'sine giden named HttpClient ve resilience tanımı.
/// </summary>
/// <remarks>
/// <b>Güvenlik:</b> Telegram, bot token'ını URL yolunda taşır
/// (<c>/bot&lt;token&gt;/sendMessage</c>). HttpClient'ın varsayılan günlükleyicisi istek
/// URI'sini yazdığından token log dosyalarına sızardı. Bu yüzden bu istemcide
/// <see cref="HttpClientBuilderExtensions.RemoveAllLoggers"/> ile varsayılan günlükleme
/// tamamen kapatılmıştır; gönderim sonucu, URI içermeyen kendi log satırlarımızla raporlanır.
/// </remarks>
public static class TelegramClient
{
    public const string Name = "Telegram";

    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(45);

    private static readonly TimeSpan CircuitSamplingDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(30);
    private const int CircuitMinimumThroughput = 5;
    private const double CircuitFailureRatio = 0.5;

    public static IServiceCollection AddTelegramClient(this IServiceCollection services)
    {
        services
            .AddHttpClient(Name, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<TelegramOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
            })
            // Token URI'de taşındığı için varsayılan istek/yanıt günlüğü kapatılır.
            .RemoveAllLoggers()
            .AddStandardResilienceHandler(resilience =>
            {
                // Bildirim arka planda gönderildiğinden burada cömert davranılabilir:
                // kimseyi bekletmiyoruz, bu yüzden ısrarla yeniden denemek mantıklı.
                resilience.Retry.MaxRetryAttempts = MaxRetryAttempts;
                resilience.Retry.Delay = RetryBaseDelay;
                resilience.Retry.BackoffType = DelayBackoffType.Exponential;
                resilience.Retry.UseJitter = true;

                resilience.AttemptTimeout.Timeout = AttemptTimeout;
                resilience.TotalRequestTimeout.Timeout = TotalRequestTimeout;

                resilience.CircuitBreaker.SamplingDuration = CircuitSamplingDuration;
                resilience.CircuitBreaker.BreakDuration = CircuitBreakDuration;
                resilience.CircuitBreaker.MinimumThroughput = CircuitMinimumThroughput;
                resilience.CircuitBreaker.FailureRatio = CircuitFailureRatio;
            });

        return services;
    }
}
