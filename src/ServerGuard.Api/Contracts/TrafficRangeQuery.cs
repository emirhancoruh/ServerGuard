using ServerGuard.Shared;

namespace ServerGuard.Api.Contracts;

/// <summary>
/// Sunucu ve zaman aralığıyla sınırlanan sorgular için ortak parametreler.
/// </summary>
public sealed record TrafficRangeQuery
{
    public string? ServerName { get; init; }

    /// <summary>Şu andan geriye doğru kaç dakikalık aralığın kapsanacağı.</summary>
    public int Minutes { get; init; } = TrafficQueryConstraints.DefaultMinutes;
}
