using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Notifications;

/// <summary>
/// Üretilen alarmı bildirim akışına duyurur.
/// </summary>
/// <remarks>
/// <see cref="Publish"/> <b>senkron ve bloklamayan</b> bir işlemdir: alarmı kuyruğa bırakır
/// ve hemen döner. Gerçek gönderim ayrı bir arka plan servisinde yapılır. Böylece dış servisin
/// yavaşlığı veya erişilemezliği, alarmı kaydeden isteği hiçbir şekilde etkilemez.
/// </remarks>
public interface IAlertEventPublisher
{
    void Publish(SecurityAlertDto alert);
}
