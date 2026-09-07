using ServerGuard.Shared.Enums;

namespace ServerGuard.Api.Data.Entities;

/// <summary>
/// SecurityAlertDto'nun veritabanı karşılığı. Id ve CreatedAt yalnızca kalıcı katmana aittir.
/// </summary>
public sealed class SecurityAlert
{
    public long Id { get; init; }
    public required string ServerName { get; init; }
    public required AlertType AlertType { get; init; }
    public required AlertSeverity Severity { get; init; }
    public required string SourceIp { get; init; }

    /// <summary>Kuralın pencere içinde saydığı olay adedi; anlamı alarm tipine göre değişir.</summary>
    public required int ObservedCount { get; init; }

    public required string Description { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Dış itibar servisinden gelen 0-100 arası skor. Bilgi alınamadıysa <c>null</c> kalır;
    /// "skor yok" ile "skor sıfır" farklı anlamlar taşıdığı için alan nullable'dır.
    /// </summary>
    public int? AbuseConfidenceScore { get; init; }
}
