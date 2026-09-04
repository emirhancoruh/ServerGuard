using ServerGuard.Shared.Enums;

namespace ServerGuard.Api.Data.Entities;

/// <summary>
/// SecurityEventDto'nun veritabanı karşılığı. Id ve CreatedAt yalnızca kalıcı katmana aittir.
/// </summary>
public sealed class SecurityEvent
{
    public long Id { get; init; }
    public required string ServerName { get; init; }
    public required SecurityEventType EventType { get; init; }
    public required string SourceIp { get; init; }
    public required string Username { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
