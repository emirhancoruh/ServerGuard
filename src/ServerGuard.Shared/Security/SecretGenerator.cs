using System.Security.Cryptography;

namespace ServerGuard.Shared.Security;

/// <summary>
/// Kriptografik olarak güvenli rastgele sır üretir (agent API anahtarı, JWT imza anahtarı).
/// </summary>
public static class SecretGenerator
{
    private const int DefaultByteCount = 32;

    /// <summary>
    /// URL ve ortam değişkeni içinde sorun çıkarmayan bir anahtar üretir.
    /// Base64'ün <c>+</c>, <c>/</c>, <c>=</c> karakterleri kabuk ve YAML'de kaçış gerektirdiğinden
    /// URL-safe alfabeye çevrilir.
    /// </summary>
    public static string Create(int byteCount = DefaultByteCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(byteCount, 1);

        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteCount))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
