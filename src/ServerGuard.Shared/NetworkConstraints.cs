namespace ServerGuard.Shared;

/// <summary>
/// Ağ alanlarının ortak sınırları.
/// </summary>
public static class NetworkConstraints
{
    /// <summary>IPv6 adresleri ve "%scope" ekleri için yeterli uzunluk.</summary>
    public const int IpAddressMaxLength = 64;
}
