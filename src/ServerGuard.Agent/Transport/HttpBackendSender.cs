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
    TimeProvider timeProvider,
    ILogger<HttpBackendSender> logger) : IBackendSender
{
    /// <summary>
    /// Yetki hatası her gönderimde tekrarlanacağından, log'u boğmamak için bu aralıkta
    /// en fazla bir kez yazılır.
    /// </summary>
    private static readonly TimeSpan AuthFailureLogInterval = TimeSpan.FromMinutes(1);

    private DateTimeOffset _lastAuthFailureLoggedAt = DateTimeOffset.MinValue;

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

            // Yetki hatası verinin bozuk olduğu anlamına gelmez, yapılandırmanın yanlış olduğu
            // anlamına gelir. Kaydı atmak veriyi kalıcı olarak kaybettirir; bu yüzden kuyrukta
            // tutulur ve anahtar düzeltildiğinde birikmiş kayıtlar gönderilir.
            if (IsAuthenticationFailure(response.StatusCode))
            {
                ReportAuthenticationFailure(route, response.StatusCode);
                return SendResult.Unavailable;
            }

            // Hız sınırına takılmak da geçicidir; sunucu "sonra tekrar dene" diyor.
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning(
                    "Backend rate limit reached for {Route}; payload will be retried later.",
                    route);
                return SendResult.Unavailable;
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

    private void ReportAuthenticationFailure(string route, HttpStatusCode statusCode)
    {
        var now = timeProvider.GetUtcNow();

        if (now - _lastAuthFailureLoggedAt < AuthFailureLogInterval)
        {
            return;
        }

        _lastAuthFailureLoggedAt = now;

        logger.LogError(
            "Backend rejected the agent API key. Route={Route} StatusCode={StatusCode}. " +
            "Set Agent:ApiKey in appsettings.json to a key listed under Security:Ingest:ApiKeys on the API. " +
            "Records are kept in the local queue until this is fixed.",
            route,
            (int)statusCode);
    }

    private static bool IsAuthenticationFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    private static bool IsClientError(HttpStatusCode statusCode) =>
        statusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError;

    private static bool IsTransient(Exception exception) =>
        exception is HttpRequestException
            or BrokenCircuitException
            or TimeoutRejectedException
            or TaskCanceledException;
}
