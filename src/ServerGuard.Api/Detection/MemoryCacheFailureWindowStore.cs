using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Pencereleri bellek içi bir cache'te tutar ve iki ayrı sınırla büyümeyi engeller:
/// her giriş <c>SlidingExpiration</c> ile tanımlıdır (pencere süresince sessiz kalan IP düşer)
/// ve cache'in toplam giriş sayısı <c>SizeLimit</c> ile sınırlıdır (sahte IP seli belleği şişiremez).
/// </summary>
/// <remarks>
/// Tespit kendi <see cref="MemoryCache"/> örneğini kullanır; böylece uygulamanın diğer cache
/// kullanımları tespit sayaçlarını tahliye edemez, tespit de onları tahliye edemez.
/// </remarks>
public sealed class MemoryCacheFailureWindowStore : IFailureWindowStore, IDisposable
{
    private const string KeyPrefix = "brute-force:failures";

    /// <summary>Her pencere cache içinde bir birim yer kaplar; sınır, izlenen IP sayısıdır.</summary>
    private const int EntrySize = 1;

    private readonly MemoryCache _cache;
    private readonly BruteForceOptions _options;

    /// <summary>
    /// Pencere nesnesinin oluşturulmasını serileştirir. Cache'in get-or-create adımı atomik değildir;
    /// kilit olmadan iki istek ayrı pencere oluşturup birinin saydığı denemeler kaybolabilirdi.
    /// Kilit yalnızca ilk oluşturmada işletilir, her istekte değil.
    /// </summary>
    private readonly Lock _creationGate = new();

    public MemoryCacheFailureWindowStore(IOptions<BruteForceOptions> options)
    {
        _options = options.Value;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.TrackedIpLimit });
    }

    public bool TryRegisterFailure(string serverName, string sourceIp, DateTimeOffset occurredAt, out int attemptsInWindow) =>
        GetOrCreateWindow(BuildKey(serverName, sourceIp))
            .TryRegisterFailure(occurredAt, _options.Window, out attemptsInWindow);

    public void Dispose() => _cache.Dispose();

    private FailureWindow GetOrCreateWindow(string key)
    {
        if (_cache.TryGetValue(key, out FailureWindow? existing) && existing is not null)
        {
            return existing;
        }

        lock (_creationGate)
        {
            if (_cache.TryGetValue(key, out FailureWindow? current) && current is not null)
            {
                return current;
            }

            var created = new FailureWindow(_options.FailureThreshold);

            _cache.Set(key, created, new MemoryCacheEntryOptions
            {
                SlidingExpiration = _options.Window,
                Size = EntrySize
            });

            return created;
        }
    }

    private static string BuildKey(string serverName, string sourceIp) =>
        $"{KeyPrefix}:{serverName}:{sourceIp}";
}
