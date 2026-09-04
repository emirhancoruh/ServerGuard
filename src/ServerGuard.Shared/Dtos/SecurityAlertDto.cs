using ServerGuard.Shared.Enums;

namespace ServerGuard.Shared.Dtos;

/// <summary>
/// Bir tespit kuralının ürettiği alarm. Panele canlı yayınlanır ve sorgulanabilir.
/// </summary>
/// <param name="Id">
/// Kalıcı kayıt kimliği; canlı yayın ile geçmiş sorgusundan gelen aynı alarm bu sayede eşlenebilir.
/// </param>
/// <param name="ObservedCount">
/// Kuralın pencere içinde saydığı olay adedi. Anlamı <paramref name="AlertType"/>'a göre değişir:
/// brute-force için başarısız giriş sayısı, trafik anomalisi için istek sayısı.
/// </param>
public sealed record SecurityAlertDto(
    long Id,
    string ServerName,
    AlertType AlertType,
    AlertSeverity Severity,
    string SourceIp,
    int ObservedCount,
    string Description,
    DateTimeOffset Timestamp) : IServerPayload;
