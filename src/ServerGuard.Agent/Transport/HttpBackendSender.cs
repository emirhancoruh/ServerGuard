using System.Net;
using System.Net.Http.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;
using ServerGuard.Shared;

namespace ServerGuard.Agent.Transport;

/// <summary>
/// Kayıtları named HttpClient üzerinden backend'e POST eder. Retry/timeout/circuit breaker
/// handler'da tanımlıdır; burada yalnızca sonucun sınıflandırılması yapılır.
/// </summary>
public sealed class HttpBackendSender(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpBackendSender> logger) : IBackendSender
{
    public async Task<SendResult> SendAsync<T>(string route, T payload, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(BackendHttpClient.Name);

        try
        {
            using var response = await client.PostAsJsonAsync(route, payload, JsonDefaults.Options, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return SendResult.Sent;
            }

            if (IsClientError(response.StatusCode))
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "Backend rejected payload. Route={Route} StatusCode={StatusCode} Body={Body}",
                    route,
                    (int)response.StatusCode,
                    body);
                return SendResult.Rejected;
            }

            logger.LogWarning(
                "Backend returned {StatusCode} for {Route}; payload will be retried later.",
                (int)response.StatusCode,
                route);
            return SendResult.Unavailable;
        }
        catch (Exception exception) when (IsTransient(exception) && !cancellationToken.IsCancellationRequested)
        {
            // Beklenen bir durum; stack trace yerine yalnızca sebep loglanır.
            logger.LogWarning(
                "Backend unreachable ({Reason}: {Message}); payload will be retried later.",
                exception.GetType().Name,
                exception.Message);
            return SendResult.Unavailable;
        }
    }

    private static bool IsClientError(HttpStatusCode statusCode) =>
        statusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError;

    private static bool IsTransient(Exception exception) =>
        exception is HttpRequestException
            or BrokenCircuitException
            or TimeoutRejectedException
            or TaskCanceledException;
}
