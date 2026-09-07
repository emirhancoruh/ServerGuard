using Microsoft.Extensions.Options;
using Polly;

namespace ServerGuard.Api.Reputation;

/// <summary>
/// AbuseIPDB'ye giden named HttpClient ve resilience (retry + timeout + circuit breaker) tanımı.
/// </summary>
/// <remarks>
/// Süreler bilinçli olarak kısa tutulmuştur: bu çağrı bir alarm üretilirken yapılır ve
/// <c>TotalRequestTimeout</c>, alarm üretiminin bekleyebileceği <b>en uzun süreyi</b> belirler.
/// Dış servis yavaşladığında gecikme buraya kadardır; sonrasında skorsuz devam edilir.
/// </remarks>
public static class AbuseIpDbClient
{
    public const string Name = "AbuseIpDb";

    private const string ApiKeyHeader = "Key";
    private const string AcceptHeader = "application/json";

    private const int MaxRetryAttempts = 2;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan CircuitSamplingDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromMinutes(1);
    private const int CircuitMinimumThroughput = 5;
    private const double CircuitFailureRatio = 0.5;

    public static IServiceCollection AddAbuseIpDbClient(this IServiceCollection services)
    {
        services
            .AddHttpClient(Name, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<IpReputationOptions>>().Value;

                client.BaseAddress = options.BaseAddress;
                client.DefaultRequestHeaders.Accept.ParseAdd(AcceptHeader);

                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    // Anahtar yalnızca istek başlığına konur; hiçbir log satırına yazılmaz.
                    client.DefaultRequestHeaders.Add(ApiKeyHeader, options.ApiKey);
                }
            })
            .AddStandardResilienceHandler(resilience =>
            {
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
