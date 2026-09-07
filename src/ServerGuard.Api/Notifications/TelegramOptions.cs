using System.ComponentModel.DataAnnotations;
using ServerGuard.Shared.Enums;

namespace ServerGuard.Api.Notifications;

/// <summary>
/// Telegram bildirim ayarları.
/// </summary>
/// <remarks>
/// <b>BotToken buraya YAZILMAZ.</b> Geliştirmede <c>dotnet user-secrets</c>, production'da
/// <c>Notifications__Telegram__BotToken</c> ortam değişkeni ile verilir. Token veya chat kimliği
/// tanımlı değilse bildirim sessizce devre dışı kalır; sistem bildirimsiz çalışmayı sürdürür.
/// </remarks>
public sealed class TelegramOptions
{
    public const string SectionName = "Notifications:Telegram";

    private const int MinQueueCapacity = 1;
    private const int MaxQueueCapacity = 100_000;

    public bool Enabled { get; set; } = true;

    /// <summary>Yalnızca sır deposundan okunur; appsettings.json'da boş kalır.</summary>
    public string? BotToken { get; set; }

    /// <summary>Bildirimlerin gönderileceği sohbet kimliği.</summary>
    public string? ChatId { get; set; }

    public Uri BaseAddress { get; set; } = new("https://api.telegram.org/");

    /// <summary>
    /// Bu seviyeden itibaren bildirim gönderilir. Varsayılan <see cref="AlertSeverity.High"/>
    /// olduğundan High ve Critical alarmlar bildirilir; ileride Critical üreten bir kural
    /// eklendiğinde ayrıca kod değişikliği gerekmez.
    /// </summary>
    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.High;

    /// <summary>
    /// Telegram erişilemezken bellekte tutulacak en fazla bildirim. Sınır aşılırsa en eski
    /// bildirim loglanarak atılır; kuyruk sınırsız büyümez.
    /// </summary>
    [Range(MinQueueCapacity, MaxQueueCapacity)]
    public int QueueCapacity { get; set; } = 500;
}
