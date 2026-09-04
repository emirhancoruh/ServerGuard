using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// W3C Extended Log Format satırını <see cref="TrafficLogDto"/>'ya çevirir.
/// Eksik veya beklenmedik biçimli satırlarda exception fırlatmaz, false döner.
/// </summary>
public sealed class W3CLogParser(ILogger<W3CLogParser> logger)
{
    public bool TryParse(
        string line,
        W3CFieldMap fields,
        string serverName,
        DateTimeOffset fallbackTimestamp,
        [NotNullWhen(true)] out TrafficLogDto? trafficLog)
    {
        trafficLog = null;

        try
        {
            var columns = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (!TryReadText(columns, fields, W3CFields.ClientIp, out var clientIp) ||
                !TryReadText(columns, fields, W3CFields.UriStem, out var requestPath) ||
                !TryReadInt32(columns, fields, W3CFields.Status, out var statusCode) ||
                !TryReadInt64(columns, fields, W3CFields.TimeTaken, out var responseTimeMs))
            {
                logger.LogWarning("Traffic line skipped: required fields missing or unreadable.");
                return false;
            }

            trafficLog = new TrafficLogDto(
                serverName,
                Truncate(clientIp, TrafficConstraints.ClientIpMaxLength),
                Truncate(requestPath, TrafficConstraints.RequestPathMaxLength),
                statusCode,
                responseTimeMs,
                ReadTimestamp(columns, fields, fallbackTimestamp));

            return true;
        }
        catch (Exception exception)
        {
            // Tek bir bozuk satır okuma döngüsünü durdurmamalı.
            logger.LogWarning("Traffic line skipped: {Reason}: {Message}", exception.GetType().Name, exception.Message);
            return false;
        }
    }

    private static bool TryReadText(string[] columns, W3CFieldMap fields, string field, [NotNullWhen(true)] out string? value)
    {
        value = null;
        var index = fields.IndexOf(field);

        if (index < 0 || index >= columns.Length)
        {
            return false;
        }

        var raw = columns[index];

        if (raw.Length == 0 || raw == W3CFields.NotApplicable)
        {
            return false;
        }

        value = raw;
        return true;
    }

    private static bool TryReadInt32(string[] columns, W3CFieldMap fields, string field, out int value)
    {
        value = 0;

        return TryReadText(columns, fields, field, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadInt64(string[] columns, W3CFieldMap fields, string field, out long value)
    {
        value = 0;

        return TryReadText(columns, fields, field, out var raw)
            && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// IIS tarih ve saati ayrı sütunlara UTC olarak yazar. Okunamazsa çağıranın verdiği zaman kullanılır.
    /// </summary>
    private static DateTimeOffset ReadTimestamp(string[] columns, W3CFieldMap fields, DateTimeOffset fallback)
    {
        if (!TryReadText(columns, fields, W3CFields.Date, out var date) ||
            !TryReadText(columns, fields, W3CFields.Time, out var time))
        {
            return fallback;
        }

        return DateTimeOffset.TryParse(
            $"{date}T{time}Z",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : fallback;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
