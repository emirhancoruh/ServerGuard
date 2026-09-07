using ServerGuard.Api.Data.Entities;
using ServerGuard.Api.Mapping;
using ServerGuard.Api.Notifications;
using ServerGuard.Api.Realtime;
using ServerGuard.Api.Repositories;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Alarmın kaydedilme akışı: önce kalıcı hale getir, sonra yan etkileri tetikle.
/// </summary>
/// <remarks>
/// Sıra bilinçlidir. Kayıt ilk adımdır; yayın ve bildirim ondan sonra gelir ve ikisi de
/// <b>başarısız olsa bile kaydı geri almaz</b>. Yayınlanan alarm kalıcı Id'yi taşır, böylece
/// panel canlı gelen alarmla geçmiş sorgusundan geleni aynı kayıt olarak eşleyebilir.
/// </remarks>
public sealed class AlertRaiser(
    ISecurityAlertRepository alertRepository,
    IMonitoringBroadcaster broadcaster,
    IAlertEventPublisher alertEvents) : IAlertRaiser
{
    public async Task<SecurityAlertDto> RaiseAsync(SecurityAlert alert, CancellationToken cancellationToken)
    {
        var saved = await alertRepository.AddAsync(alert, cancellationToken);
        var dto = saved.ToDto();

        // Panele canlı yayın: kendi içinde hataya toleranslıdır, exception sızdırmaz.
        await broadcaster.BroadcastAlertAsync(dto, cancellationToken);

        // Dış bildirim: yalnızca kuyruğa bırakılır ve hemen dönülür. Telegram'ın yavaşlığı
        // veya erişilemezliği bu isteği hiçbir şekilde bekletmez.
        alertEvents.Publish(dto);

        return dto;
    }
}
