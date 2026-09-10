namespace ServerGuard.Api.Hosting;

/// <summary>
/// İstek gövdesi sınırları.
/// </summary>
/// <remarks>
/// Agent'ın gönderdiği en büyük kayıt birkaç yüz bayttır. Varsayılan 30 MB sınırı,
/// tek bir isteğin belleği ve ağı gereksiz yere meşgul etmesine izin verir; sınır
/// gerçek ihtiyaca çekilir.
/// </remarks>
public static class RequestLimits
{
    private const int Kilobyte = 1024;

    public const long MaxRequestBodyBytes = 256 * Kilobyte;
}
