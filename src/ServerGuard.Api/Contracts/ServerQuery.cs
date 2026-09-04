using ServerGuard.Shared;

namespace ServerGuard.Api.Contracts;

/// <summary><c>GET /api/servers</c> sorgu parametreleri.</summary>
public sealed record ServerQuery
{
    /// <summary>
    /// Kaç saat geriye kadar veri göndermiş sunucuların listeleneceği.
    /// Aralığı sınırlamak, sorgunun tüm tabloyu taramasını engeller.
    /// </summary>
    public int SinceHours { get; init; } = ServerQueryConstraints.DefaultSinceHours;
}
