namespace ServerGuard.Shared;

/// <summary>
/// Güvenlik olayı alanlarının ortak sınırları.
/// </summary>
public static class SecurityEventConstraints
{
    public const int SourceIpMaxLength = NetworkConstraints.IpAddressMaxLength;

    /// <summary>Windows kullanıcı adı sınırı, domain öneki için pay bırakılmıştır.</summary>
    public const int UsernameMaxLength = 256;

    /// <summary>Kaynak IP'nin bilinmediği yerel/servis oturumları için kullanılan değer.</summary>
    public const string LocalSourceIp = "local";

    /// <summary>Kullanıcı adının olay içinde boş geldiği durumlar için kullanılan değer.</summary>
    public const string UnknownUsername = "unknown";
}
