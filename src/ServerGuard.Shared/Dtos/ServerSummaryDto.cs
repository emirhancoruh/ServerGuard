namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Sistemin tanıdığı bir sunucu. <paramref name="LastSeenAt"/>, o sunucudan gelen
/// en son kaydın zamanıdır; sunucunun hâlâ veri gönderip göndermediğini gösterir.
/// </summary>
public sealed record ServerSummaryDto(string ServerName, DateTimeOffset LastSeenAt);
