using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Gelen trafik kaydını anormal istek yoğunluğu kuralına göre inceler; eşik aşılırsa alarm üretir,
/// kaydeder ve yayınlar. Asla exception fırlatmaz — tespit hatası trafik kaydını etkilemez.
/// </summary>
public interface ITrafficAnomalyDetectionService
{
    Task InspectAsync(TrafficLogDto trafficLog, CancellationToken cancellationToken);
}
