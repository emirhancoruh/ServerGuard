using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using ServerGuard.Shared;

namespace ServerGuard.Api.Reputation;

/// <summary>
/// AbuseIPDB üzerinden IP itibarı sorgular.
/// </summary>
/// <remarks>
/// <para>
/// Bu servis <b>en iyi çaba (best effort)</b> ilkesiyle çalışır: skor gelirse alarmı zenginleştirir,
/// gelmezse hiçbir şeyi bozmaz. Hiçbir hata dışarı sızmaz, dönüş değeri en kötü ihtimalle
/// <c>null</c> olur.
/// </para>
/// <para>
/// Süre sınırı named HttpClient üzerindeki resilience pipeline'ının <c>TotalRequestTimeout</c>
/// değeriyle garanti edilir; AbuseIPDB yanıt vermese veya çok yavaş olsa bile çağrı bu süreyi
/// aşamaz ve alarm üretimi kilitlenmez.
/// </para>
/// </remarks>
public sealed class AbuseIpDbReputationService : IIpReputationService, IDisposable
{
    private const string CheckEndpoint = "check";
    private const string IpAddressParameter = "ipAddress";
    private const string MaxAgeParameter = "maxAgeInDays";
    private const string CacheKeyPrefix = "ip-reputation";

    /// <summary>Her adres önbellekte bir birim yer kaplar; sınır, izlenen adres sayısıdır.</summary>
    private const int CacheEntrySize = 1;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IpReputationOptions _options;
    private readonly ILogger<AbuseIpDbReputationService> _logger;
    private readonly MemoryCache _cache;
    private readonly bool _isConfigured;

    public AbuseIpDbReputationService(
        IHttpClientFactory httpClientFactory,
        IOptions<IpReputationOptions> options,
        ILogger<AbuseIpDbReputationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.CachedIpLimit });

        _isConfigured = _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

        if (_options.Enabled && !_isConfigured)
        {
            _logger.LogInformation(
                "IP reputation lookup is enabled but no API key is configured; scores will be omitted. " +
                "Set {SectionName}:ApiKey via user-secrets or the Detection__IpReputation__ApiKey environment variable.",
                IpReputationOptions.SectionName);
        }
    }

    public async Task<int?> TryGetAbuseScoreAsync(string ipAddress, CancellationToken cancellationToken)
    {
        if (!_isConfigured || !IsPublicIpAddress(ipAddress))
        {
            return null;
        }

        var cacheKey = $"{CacheKeyPrefix}:{ipAddress}";

        if (_cache.TryGetValue(cacheKey, out int? cachedScore))
        {
            return cachedScore;
        }

        var score = await QueryAsync(ipAddress, cancellationToken);

        if (score is not null)
        {
            // Mutlak süre kullanılır, kayan süre değil: skor belirli bir ana ait bir olgudur.
            // Kayan süre, sık görülen bir adres için eskimiş skoru süresiz taze tutardı.
            _cache.Set(cacheKey, score, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _options.CacheDuration,
                Size = CacheEntrySize
            });
        }

        return score;
    }

    public void Dispose() => _cache.Dispose();

    private async Task<int?> QueryAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(AbuseIpDbClient.Name);
            var requestUri = $"{CheckEndpoint}?{IpAddressParameter}={Uri.EscapeDataString(ipAddress)}" +
                             $"&{MaxAgeParameter}={_options.MaxAgeInDays}";

            using var response = await client.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning(
                    "AbuseIPDB rate limit reached; reputation lookups will be skipped until the quota resets. Ip={Ip}",
                    ipAddress);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AbuseIPDB returned {StatusCode} for {Ip}; alert will be recorded without a reputation score.",
                    (int)response.StatusCode,
                    ipAddress);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<AbuseIpDbCheckResponse>(
                JsonDefaults.Options,
                cancellationToken);

            var score = payload?.Data?.AbuseConfidenceScore;

            if (score is null or < AlertConstraints.MinAbuseConfidenceScore or > AlertConstraints.MaxAbuseConfidenceScore)
            {
                _logger.LogWarning("AbuseIPDB returned an unusable score for {Ip}: {Score}", ipAddress, score);
                return null;
            }

            _logger.LogInformation(
                "IP reputation resolved. Ip={Ip} AbuseConfidenceScore={Score} TotalReports={Reports} Country={Country}",
                ipAddress,
                score,
                payload!.Data!.TotalReports,
                payload.Data.CountryCode);

            return score;
        }
        catch (Exception exception) when (IsExpectedFailure(exception, cancellationToken))
        {
            // Beklenen bir aksaklık; alarm üretimi bundan etkilenmemeli.
            _logger.LogWarning(
                "IP reputation lookup failed ({Reason}: {Message}); alert will be recorded without a score. Ip={Ip}",
                exception.GetType().Name,
                exception.Message,
                ipAddress);

            return null;
        }
    }

    /// <summary>
    /// İptal isteği çağırandan geldiyse (uygulama kapanıyor) hata yutulmaz; geri kalan tüm
    /// ağ/zaman aşımı/devre kesici durumları beklenen aksaklıklardır.
    /// </summary>
    private static bool IsExpectedFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested &&
        exception is HttpRequestException
            or BrokenCircuitException
            or TimeoutRejectedException
            or TaskCanceledException
            or OperationCanceledException
            or System.Text.Json.JsonException;

    /// <summary>
    /// Yalnızca genel internete ait adresler sorgulanır. Özel ağ, loopback ve link-local
    /// adresler için AbuseIPDB anlamlı sonuç dönmez; sorgulamak kotayı boşa harcar.
    /// </summary>
    private static bool IsPublicIpAddress(string value)
    {
        if (!IPAddress.TryParse(value, out var address) || IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal && !address.IsIPv6UniqueLocal;
        }

        var octets = address.GetAddressBytes();

        return octets[0] switch
        {
            0 or 127 => false,
            10 => false,
            100 when octets[1] is >= 64 and <= 127 => false,
            169 when octets[1] == 254 => false,
            172 when octets[1] is >= 16 and <= 31 => false,
            192 when octets[1] == 168 => false,
            _ => true
        };
    }
}
