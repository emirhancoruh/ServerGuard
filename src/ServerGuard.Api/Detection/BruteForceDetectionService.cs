using System.Globalization;
using Microsoft.Extensions.Options;
using ServerGuard.Api.Data.Entities;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;
using ServerGuard.Shared.Enums;

namespace ServerGuard.Api.Detection;

/// <summary>
/// Aynı kaynak IP'den yapılan başarısız giriş denemelerini sayar; yapılandırılan pencere içinde
/// eşiğe ulaşıldığında <see cref="AlertType.BruteForceAttempt"/> alarmı üretir.
/// </summary>
public sealed class BruteForceDetectionService(
    IFailureWindowStore windowStore,
    IAlertRaiser alertRaiser,
    IOptions<BruteForceOptions> options,
    TimeProvider timeProvider,
    ILogger<BruteForceDetectionService> logger) : IBruteForceDetectionService
{
    private readonly BruteForceOptions _options = options.Value;

    public async Task InspectAsync(SecurityEventDto securityEvent, CancellationToken cancellationToken)
    {
        if (!_options.Enabled || securityEvent.EventType != SecurityEventType.FailedLogin)
        {
            return;
        }

        try
        {
            var thresholdReached = windowStore.TryRegisterFailure(
                securityEvent.ServerName,
                securityEvent.SourceIp,
                securityEvent.Timestamp,
                out var attemptsInWindow);

            if (!thresholdReached)
            {
                return;
            }

            await RaiseAlertAsync(securityEvent, attemptsInWindow, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Tespit, olayın kaydedilmesini bozmamalı; olay zaten veritabanına yazıldı.
            logger.LogError(
                exception,
                "Brute-force detection failed. Server={ServerName} SourceIp={SourceIp}",
                securityEvent.ServerName,
                securityEvent.SourceIp);
        }
    }

    private async Task RaiseAlertAsync(SecurityEventDto securityEvent, int attemptsInWindow, CancellationToken cancellationToken)
    {
        var detectedAt = timeProvider.GetUtcNow();

        var alert = new SecurityAlert
        {
            ServerName = securityEvent.ServerName,
            AlertType = AlertType.BruteForceAttempt,
            Severity = AlertSeverity.High,
            SourceIp = securityEvent.SourceIp,
            ObservedCount = attemptsInWindow,
            Description = BuildDescription(securityEvent.SourceIp, attemptsInWindow),
            Timestamp = detectedAt,
            CreatedAt = detectedAt
        };

        var raised = await alertRaiser.RaiseAsync(alert, cancellationToken);

        logger.LogWarning(
            "Brute-force alert raised. AlertId={AlertId} Server={ServerName} SourceIp={SourceIp} Attempts={Attempts} Window={Window}",
            raised.Id,
            raised.ServerName,
            raised.SourceIp,
            attemptsInWindow,
            _options.Window);

    }

    private string BuildDescription(string sourceIp, int attemptsInWindow)
    {
        var description = string.Format(
            CultureInfo.InvariantCulture,
            "{0} adresinden son {1:0.#} dakika içinde {2} başarısız giriş denemesi yapıldı.",
            sourceIp,
            _options.Window.TotalMinutes,
            attemptsInWindow);

        return description.Length <= AlertConstraints.DescriptionMaxLength
            ? description
            : description[..AlertConstraints.DescriptionMaxLength];
    }
}
