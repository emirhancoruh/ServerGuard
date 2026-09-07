using System.Globalization;
using Microsoft.Extensions.Options;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Api.Reputation;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;
using ServerGuard.Shared.Enums;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Aynı kaynak IP'den gelen istekleri sayar; yapılandırılan pencere içinde eşik aşılırsa
/// <see cref="AlertType.TrafficAnomaly"/> alarmı üretir.
/// </summary>
/// <remarks>
/// <para>
/// <b>ÖNEMLİ — Bu bir DDoS koruması DEĞİLDİR.</b>
/// </para>
/// <para>
/// Burada yapılan iş yalnızca <i>gözlem</i>dir: IIS'in zaten yazdığı loglar okunduktan sonra,
/// yani istekler sunucuya çoktan ulaşıp işlendikten sonra sayılır. Hiçbir isteği engellemez,
/// yavaşlatmaz, hız sınırlaması (rate limiting) uygulamaz veya IP yasaklamaz. Tespit, olaydan
/// saniyeler hatta dakikalar sonra gerçekleşir; çünkü IIS logları tampondan periyodik olarak yazar.
/// </para>
/// <para>
/// Gerçek bir DDoS saldırısı, sunucunun kaynaklarını bu katman çalışmaya fırsat bulamadan tüketir.
/// Gerçek koruma trafiğin sunucuya ulaşmadan önceki katmanlarında yapılır: ağ/güvenlik duvarı
/// seviyesinde filtreleme, ters vekil sunucu (reverse proxy) veya CDN üzerinde hız sınırlama,
/// IIS Dynamic IP Restrictions gibi modüller, ya da sağlayıcı seviyesinde DDoS azaltma servisleri.
/// </para>
/// <para>
/// Bu sınıfın amacı, olağan dışı trafik paternini <i>fark edilebilir</i> kılmaktır: bir IP'nin
/// aniden normalin çok üzerinde istek göndermesi, tarama (scraping), hatalı yapılandırılmış bir
/// istemci, bozuk bir yeniden deneme döngüsü veya bir saldırının erken belirtisi olabilir.
/// Karar ve müdahale operatöre aittir.
/// </para>
/// </remarks>
public sealed class TrafficAnomalyDetectionService(
    ITrafficWindowStore windowStore,
    IAlertRaiser alertRaiser,
    IIpReputationService ipReputation,
    IOptions<TrafficAnomalyOptions> options,
    TimeProvider timeProvider,
    ILogger<TrafficAnomalyDetectionService> logger) : ITrafficAnomalyDetectionService
{
    private readonly TrafficAnomalyOptions _options = options.Value;

    public async Task InspectAsync(TrafficLogDto trafficLog, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        try
        {
            var thresholdExceeded = windowStore.TryRegisterRequest(
                trafficLog.ServerName,
                trafficLog.ClientIp,
                trafficLog.Timestamp,
                out var requestsInWindow);

            if (!thresholdExceeded)
            {
                return;
            }

            await RaiseAlertAsync(trafficLog, requestsInWindow, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Tespit, trafik kaydının saklanmasını bozmamalı; kayıt zaten veritabanına yazıldı.
            logger.LogError(
                exception,
                "Traffic anomaly detection failed. Server={ServerName} ClientIp={ClientIp}",
                trafficLog.ServerName,
                trafficLog.ClientIp);
        }
    }

    private async Task RaiseAlertAsync(TrafficLogDto trafficLog, int requestsInWindow, CancellationToken cancellationToken)
    {
        var detectedAt = timeProvider.GetUtcNow();

        // İtibar sorgusu en iyi çaba ilkesiyle yapılır: skor gelmezse null kalır ve alarm
        // yine de üretilir. Servis exception fırlatmaz ve süresi resilience pipeline'ıyla
        // sınırlıdır, bu yüzden alarm üretimi dış servise bağımlı hale gelmez.
        var abuseConfidenceScore = await ipReputation.TryGetAbuseScoreAsync(trafficLog.ClientIp, cancellationToken);

        var alert = new SecurityAlert
        {
            ServerName = trafficLog.ServerName,
            AlertType = AlertType.TrafficAnomaly,
            Severity = AlertSeverity.Medium,
            SourceIp = trafficLog.ClientIp,
            ObservedCount = requestsInWindow,
            Description = BuildDescription(trafficLog.ClientIp, requestsInWindow, abuseConfidenceScore),
            Timestamp = detectedAt,
            CreatedAt = detectedAt,
            AbuseConfidenceScore = abuseConfidenceScore
        };

        var raised = await alertRaiser.RaiseAsync(alert, cancellationToken);

        logger.LogWarning(
            "Traffic anomaly alert raised. AlertId={AlertId} Server={ServerName} ClientIp={ClientIp} " +
            "Requests={Requests} Window={Window} AbuseScore={AbuseScore} TrackedIps={TrackedIps}",
            raised.Id,
            raised.ServerName,
            raised.SourceIp,
            requestsInWindow,
            _options.Window,
            abuseConfidenceScore,
            windowStore.TrackedCount);

    }

    private string BuildDescription(string clientIp, int requestsInWindow, int? abuseConfidenceScore)
    {
        var description = string.Format(
            CultureInfo.InvariantCulture,
            "{0} adresinden son {1:0.#} dakika içinde {2} istek geldi (eşik: {3}).",
            clientIp,
            _options.Window.TotalMinutes,
            requestsInWindow,
            _options.RequestThreshold);

        if (abuseConfidenceScore is not null)
        {
            description += string.Format(
                CultureInfo.InvariantCulture,
                " AbuseIPDB kötüye kullanım skoru: {0}/100.",
                abuseConfidenceScore);
        }

        return description.Length <= AlertConstraints.DescriptionMaxLength
            ? description
            : description[..AlertConstraints.DescriptionMaxLength];
    }
}
