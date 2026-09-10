using Microsoft.Extensions.Options;
using Polly;
using ServerGuard.Agent.Configuration;
using ServerGuard.Shared;

namespace ServerGuard.Agent.Transport;

/// <summary>
/// Backend'e giden named HttpClient ve resilience (retry + timeout + circuit breaker) tanımı.
/// </summary>
public static class BackendHttpClient
{
    public const string Name = "Backend";

    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan CircuitSamplingDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(15);
    private const int CircuitMinimumThroughput = 5;
    private const double CircuitFailureRatio = 0.5;

    public static IServiceCollection AddBackendHttpClient(this IServiceCollection services)
    {
        services
            .AddHttpClient(Name, (serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AgentOptions>>().Value;
                client.BaseAddress = options.ApiBaseUrl;

                // Anahtar her istekte gider; tek tek çağrıların bunu hatırlaması gerekmez.
                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    client.DefaultRequestHeaders.Add(AuthConstraints.ApiKeyHeaderName, options.ApiKey);
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
