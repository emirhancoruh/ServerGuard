namespace ServerGuard.Shared;

/// <summary>
/// Trafik kaydı alanlarının ortak sınırları.
/// </summary>
public static class TrafficConstraints
{
    public const int ClientIpMaxLength = NetworkConstraints.IpAddressMaxLength;

    /// <summary>IIS'in varsayılan URL sınırıyla uyumlu istek yolu uzunluğu.</summary>
    public const int RequestPathMaxLength = 2048;

    public const int MinStatusCode = 100;
    public const int MaxStatusCode = 599;

    public const long MinResponseTimeMs = 0;

    /// <summary>Bir günü aşan yanıt süresi geçerli kabul edilmez.</summary>
    public const long MaxResponseTimeMs = 86_400_000;
}
