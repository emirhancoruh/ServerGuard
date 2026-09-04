using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Gelen güvenlik olayını brute-force kuralına göre inceler; eşik aşılırsa alarm üretir,
/// kaydeder ve yayınlar. Asla exception fırlatmaz — tespit hatası olayın kaydını etkilemez.
/// </summary>
public interface IBruteForceDetectionService
{
    Task InspectAsync(SecurityEventDto securityEvent, CancellationToken cancellationToken);
}
