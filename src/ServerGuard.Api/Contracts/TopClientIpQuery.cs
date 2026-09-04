using ServerGuard.Shared;

namespace ServerGuard.Api.Contracts;

/// <summary><c>GET /api/traffic/top-ips</c> sorgu parametreleri.</summary>
public sealed record TopClientIpQuery
{
    public string? ServerName { get; init; }

    /// <summary>Şu andan geriye doğru kaç dakikalık aralığın sayılacağı.</summary>
    public int Minutes { get; init; } = TrafficQueryConstraints.DefaultMinutes;

    /// <summary>Kaç adres döneceği.</summary>
    public int Take { get; init; } = TrafficQueryConstraints.DefaultTake;
}
