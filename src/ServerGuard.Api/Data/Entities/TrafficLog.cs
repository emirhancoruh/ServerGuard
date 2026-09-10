namespace ServerGuard.Api.Data.Entities;

/// <summary>
/// TrafficLogDto'nun veritabanı karşılığı. Id ve CreatedAt yalnızca kalıcı katmana aittir.
/// </summary>
public sealed class TrafficLog : IRetainedRecord
{
    public long Id { get; init; }
    public required string ServerName { get; init; }
    public required string ClientIp { get; init; }
    public required string RequestPath { get; init; }
    public required int StatusCode { get; init; }
    public required long ResponseTimeMs { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
