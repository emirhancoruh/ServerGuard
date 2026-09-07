using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Notifications;

/// <summary>
/// Alarmı Telegram Bot API üzerinden önceden tanımlı bir sohbete gönderir.
/// </summary>
/// <remarks>
/// Asla exception fırlatmaz. Gönderim başarısız olursa yalnızca loglanır; alarm kaydı
/// çoktan veritabanına yazılmıştır ve bildirim hatası onu geçersiz kılmaz.
/// </remarks>
public sealed class TelegramNotificationService(
    IHttpClientFactory httpClientFactory,
    IOptions<TelegramOptions> options,
    ILogger<TelegramNotificationService> logger) : IAlertNotifier
{
    private const string SendMessageMethod = "sendMessage";
    private const string HtmlParseMode = "HTML";

    private readonly TelegramOptions _options = options.Value;

    public bool IsEnabled =>
        _options.Enabled &&
        !string.IsNullOrWhiteSpace(_options.BotToken) &&
        !string.IsNullOrWhiteSpace(_options.ChatId);

    public async Task NotifyAsync(SecurityAlertDto alert, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient(TelegramClient.Name);

            var request = new TelegramSendMessageRequest(
                _options.ChatId!,
                BuildMessage(alert),
                HtmlParseMode);

            using var response = await client.PostAsJsonAsync(
                BuildRequestPath(_options.BotToken!),
                request,
                JsonDefaults.Options,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Telegram notification sent. AlertId={AlertId}", alert.Id);
                return;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning(
                    "Telegram rate limit reached; notification dropped. AlertId={AlertId}",
                    alert.Id);
                return;
            }

            // Yanıt gövdesi hata sebebini içerir ama token içermez; güvenle loglanabilir.
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogWarning(
                "Telegram notification rejected. StatusCode={StatusCode} AlertId={AlertId} Detail={Detail}",
                (int)response.StatusCode,
                alert.Id,
                body);
        }
        catch (Exception exception) when (IsExpectedFailure(exception, cancellationToken))
        {
            // Beklenen bir aksaklık; alarm kaydı bundan etkilenmez.
            logger.LogWarning(
                "Telegram notification failed ({Reason}: {Message}); the alert record is unaffected. AlertId={AlertId}",
                exception.GetType().Name,
                exception.Message,
                alert.Id);
        }
    }

    /// <summary>
    /// İstek yolunu kök göreli (<c>/bot...</c>) olarak kurar.
    /// </summary>
    /// <remarks>
    /// Telegram token'ı <c>&lt;bot_id&gt;:&lt;secret&gt;</c> biçimindedir ve <b>iki nokta içerir</b>.
    /// Yol baştaki eğik çizgi olmadan verilseydi, <c>bot123456:...</c> ifadesindeki iki nokta
    /// yüzünden URI ayrıştırıcısı <c>bot123456</c>'yı bir şema sanar ve istek
    /// "scheme is not supported" hatasıyla düşerdi.
    /// </remarks>
    private static string BuildRequestPath(string botToken) => $"/bot{botToken}/{SendMessageMethod}";

    private static bool IsExpectedFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested &&
        exception is HttpRequestException
            or BrokenCircuitException
            or TimeoutRejectedException
            or TaskCanceledException
            or OperationCanceledException;

    private static string BuildMessage(SecurityAlertDto alert)
    {
        var message = new StringBuilder()
            .AppendLine($"🚨 <b>ServerGuard — {Escape(alert.Severity.ToString())} öncelikli alarm</b>")
            .AppendLine()
            .AppendLine($"<b>Sunucu:</b> {Escape(alert.ServerName)}")
            .AppendLine($"<b>Tip:</b> {Escape(alert.AlertType.ToString())}")
            .AppendLine($"<b>Kaynak IP:</b> <code>{Escape(alert.SourceIp)}</code>")
            .AppendLine($"<b>Sayı:</b> {alert.ObservedCount}")
            .AppendLine($"<b>Zaman:</b> {alert.Timestamp.UtcDateTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)} UTC");

        if (alert.AbuseConfidenceScore is not null)
        {
            message.AppendLine($"<b>AbuseIPDB:</b> {alert.AbuseConfidenceScore}/100");
        }

        return message
            .AppendLine()
            .Append($"<i>{Escape(alert.Description)}</i>")
            .ToString();
    }

    /// <summary>
    /// HTML biçimlendirme kullanıldığından, dışarıdan gelen değerlerdeki (sunucu adı, IP,
    /// açıklama) özel karakterler kaçırılmalıdır; aksi halde mesaj bozulur veya beklenmedik
    /// biçimlendirme oluşur.
    /// </summary>
    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
}
