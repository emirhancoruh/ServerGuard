namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Zaman serisinin tek bir dilimi. <paramref name="Timestamp"/> dilimin başlangıcıdır.
/// İstek gelmeyen dilimler de sıfır sayacıyla döner; grafikte kopukluk oluşmaz.
/// </summary>
public sealed record TrafficTimelinePointDto(DateTimeOffset Timestamp, int RequestCount);
