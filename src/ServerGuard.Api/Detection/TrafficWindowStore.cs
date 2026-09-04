using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Sayaçları eşzamanlı bir sözlükte tutar. Boşta kalan IP'ler
/// <see cref="TrafficWindowCleanupService"/> tarafından periyodik olarak silinir.
/// </summary>
/// <remarks>
/// Brute-force tespitindeki gibi <c>IMemoryCache</c> yerine <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// kullanılır; çünkü burada temizlik ölçütü "en son ne zaman görüldü" bilgisidir ve taramanın kaç kayıt
/// sildiği gözlemlenebilir olmalıdır. Ayrıca <c>GetOrAdd</c>, sözlükte gerçekten duran örneği döndürür;
/// bu yüzden <c>IMemoryCache.GetOrCreate</c>'in aksine sayım kaybına yol açmaz ve ek kilit gerektirmez.
/// </remarks>
public sealed class TrafficWindowStore(
    IOptions<TrafficAnomalyOptions> options,
    TimeProvider timeProvider,
    ILogger<TrafficWindowStore> logger) : ITrafficWindowStore
{
    private readonly ConcurrentDictionary<string, TrafficWindow> _windows = new(StringComparer.OrdinalIgnoreCase);
    private readonly TrafficAnomalyOptions _options = options.Value;

    private DateTimeOffset _limitWarningLoggedAt = DateTimeOffset.MinValue;

    public int TrackedCount => _windows.Count;

    public bool TryRegisterRequest(string serverName, string clientIp, DateTimeOffset occurredAt, out int requestsInWindow)
    {
        requestsInWindow = 0;

        var key = BuildKey(serverName, clientIp);
        var observedAt = timeProvider.GetUtcNow();

        if (!_windows.TryGetValue(key, out var window))
        {
            if (_windows.Count >= _options.TrackedIpLimit)
            {
                ReportLimitReached(observedAt);
                return false;
            }

            window = _windows.GetOrAdd(key, _ => new TrafficWindow(_options.Window, _options.BucketCount, observedAt));
        }

        return window.TryRegisterRequest(
            occurredAt,
            observedAt,
            _options.RequestThreshold,
            _options.AlertCooldown,
            out requestsInWindow);
    }

    public int RemoveIdle(DateTimeOffset now, TimeSpan idleRetention)
    {
        var cutoff = now - idleRetention;
        var removed = 0;

        foreach (var (key, window) in _windows)
        {
            if (window.LastSeenAt > cutoff)
            {
                continue;
            }

            // Silme ile eşzamanlı gelen bir istek sayacı tazelemiş olabilir; son bir kez doğrulanır.
            if (window.LastSeenAt <= cutoff && _windows.TryRemove(new KeyValuePair<string, TrafficWindow>(key, window)))
            {
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Sınıra ulaşıldığında log'u boğmamak için uyarı pencere başına bir kez yazılır.</summary>
    private void ReportLimitReached(DateTimeOffset occurredAt)
    {
        if (occurredAt - _limitWarningLoggedAt < _options.Window)
        {
            return;
        }

        _limitWarningLoggedAt = occurredAt;

        logger.LogWarning(
            "Traffic anomaly tracking limit reached ({Limit}); new source addresses are not being counted until idle entries are cleaned up.",
            _options.TrackedIpLimit);
    }

    private static string BuildKey(string serverName, string clientIp) => $"{serverName}|{clientIp}";
}
