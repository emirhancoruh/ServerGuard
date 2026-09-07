using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Bir alarmın üretilmesiyle ilgili tüm adımları tek yerde toplar.
/// </summary>
/// <remarks>
/// Her tespit kuralının aynı üç adımı (kaydet, panele yayınla, bildirim kuyruğuna bırak)
/// kendi içinde tekrarlaması, yeni bir kural eklendiğinde bir adımın unutulmasına açık kapı
/// bırakırdı. Kurallar yalnızca "şu alarmı üret" der; sırayı ve garantileri burası bilir.
/// </remarks>
public interface IAlertRaiser
{
    /// <summary>
    /// Alarmı kaydeder, panele yayınlar ve bildirim akışına duyurur; kaydedilen alarmı döner.
    /// </summary>
    Task<SecurityAlertDto> RaiseAsync(SecurityAlert alert, CancellationToken cancellationToken);
}
