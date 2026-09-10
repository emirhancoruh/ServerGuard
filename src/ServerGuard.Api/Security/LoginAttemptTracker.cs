using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Security;

/// <summary>
/// Başarısız giriş denemelerini kullanıcı adına göre sayar ve eşik aşılınca geçici kilit uygular.
/// </summary>
/// <remarks>
/// Sayaçlar kendi <see cref="MemoryCache"/> örneğinde tutulur; her giriş kilit süresi kadar yaşar
/// ve toplam giriş sayısı <c>SizeLimit</c> ile sınırlıdır. Böylece rastgele kullanıcı adlarıyla
/// yapılan bir deneme seli belleği şişiremez.
/// Sayım var olmayan kullanıcı adları için de yapılır; aksi halde kilitlenip kilitlenmemesinden
/// hesabın var olup olmadığı anlaşılabilirdi.
/// </remarks>
public sealed class LoginAttemptTracker : IDisposable
{
    private const string KeyPrefix = "login-failures";
    private const int EntrySize = 1;

    private readonly MemoryCache _cache;
    private readonly PanelOptions _options;
    private readonly Lock _creationGate = new();

    public LoginAttemptTracker(IOptions<SecurityOptions> options)
    {
        _options = options.Value.Panel;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = _options.TrackedAttemptLimit });
    }

    public bool IsLockedOut(string userName) =>
        GetOrCreateCounter(userName).Count >= _options.MaxFailedAttempts;

    /// <summary>Başarısız denemeyi kaydeder ve kilit sınırına ulaşılıp ulaşılmadığını döner.</summary>
    public bool RegisterFailure(string userName) =>
        GetOrCreateCounter(userName).Increment() >= _options.MaxFailedAttempts;

    /// <summary>Başarılı girişten sonra sayaç sıfırlanır.</summary>
    public void Reset(string userName) => _cache.Remove(BuildKey(userName));

    public void Dispose() => _cache.Dispose();

    private FailedLoginCounter GetOrCreateCounter(string userName)
    {
        var key = BuildKey(userName);

        if (_cache.TryGetValue(key, out FailedLoginCounter? existing) && existing is not null)
        {
            return existing;
        }

        lock (_creationGate)
        {
            if (_cache.TryGetValue(key, out FailedLoginCounter? current) && current is not null)
            {
                return current;
            }

            var created = new FailedLoginCounter();

            _cache.Set(key, created, new MemoryCacheEntryOptions
            {
                // Kayan değil mutlak süre: kilit, denemeler sürse bile bir noktada kalkar.
                AbsoluteExpirationRelativeToNow = _options.LockoutDuration,
                Size = EntrySize
            });

            return created;
        }
    }

    /// <summary>Kullanıcı adı büyük/küçük harf duyarsız eşleştiği için anahtar da normalize edilir.</summary>
    private static string BuildKey(string userName) => $"{KeyPrefix}:{userName.ToUpperInvariant()}";

    private sealed class FailedLoginCounter
    {
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public int Increment() => Interlocked.Increment(ref _count);
    }
}
