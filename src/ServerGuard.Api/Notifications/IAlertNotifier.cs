using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Notifications;

/// <summary>
/// Alarmı bir dış kanala bildirir (Telegram, e-posta, webhook...).
/// </summary>
/// <remarks>
/// Uygulamalar <b>asla exception fırlatmamalıdır</b>; başarısızlık loglanıp yutulur.
/// Bildirim, alarmın kaydedilmesinden bağımsız bir yan etkidir ve başarısızlığı
/// kaydı geçersiz kılmaz.
/// </remarks>
public interface IAlertNotifier
{
    /// <summary>Bu bildiricinin yapılandırılmış ve çalışır durumda olup olmadığı.</summary>
    bool IsEnabled { get; }

    Task NotifyAsync(SecurityAlertDto alert, CancellationToken cancellationToken);
}
