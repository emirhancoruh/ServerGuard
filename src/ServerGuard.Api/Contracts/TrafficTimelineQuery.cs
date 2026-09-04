using ServerGuard.Shared;

namespace ServerGuard.Api.Contracts;

/// <summary><c>GET /api/traffic/timeline</c> sorgu parametreleri.</summary>
public sealed record TrafficTimelineQuery
{
    public string? ServerName { get; init; }

    /// <summary>Şu andan geriye doğru kaç dakikalık aralığın getirileceği.</summary>
    public int Minutes { get; init; } = TrafficQueryConstraints.DefaultMinutes;

    /// <summary>Her bir noktanın kaç saniyelik dilimi temsil ettiği.</summary>
    public int BucketSeconds { get; init; } = TrafficQueryConstraints.DefaultBucketSeconds;
}
