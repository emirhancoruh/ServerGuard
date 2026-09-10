namespace ServerGuard.Api.Data.Entities;

/// <summary>
/// Saklama süresi dolduğunda temizlenebilen kayıt.
/// </summary>
/// <remarks>
/// Temizlik <see cref="CreatedAt"/> alanına göre yapılır, agent'ın gönderdiği
/// <c>Timestamp</c> alanına göre değil: sunucu saati yanlış ayarlanmış veya kötü niyetli
/// bir agent, gelecekteki bir zaman damgası göndererek kayıtlarını kalıcı hale getiremesin.
/// </remarks>
public interface IRetainedRecord
{
    long Id { get; }

    DateTimeOffset CreatedAt { get; }
}
