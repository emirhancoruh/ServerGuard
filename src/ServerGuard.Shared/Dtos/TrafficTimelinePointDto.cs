namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Zaman serisinin tek bir dilimi. <paramref name="Timestamp"/> dilimin başlangıcıdır.
/// İstek gelmeyen dilimler de sıfır sayacıyla döner; grafikte kopukluk oluşmaz.
/// </summary>
/// <remarks>
/// Sayımlar HTTP durum sınıfına göre ayrılır. Toplam istek eğrisi tek başına yanıltıcıdır:
/// hepsi hata dönen bir servis, sağlıklı bir servisle aynı görünür.
/// </remarks>
public sealed record TrafficTimelinePointDto(
    DateTimeOffset Timestamp,
    int RequestCount,
    int SuccessCount,
    int ClientErrorCount,
    int ServerErrorCount);
