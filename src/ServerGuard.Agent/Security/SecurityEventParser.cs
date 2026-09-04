using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml.Linq;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;
using ServerGuard.Shared.Enums;

namespace ServerGuard.Agent.Security;

/// <summary>
/// Windows olay XML'ini <see cref="SecurityEventDto"/>'ya çevirir.
/// Bozuk veya eksik veride exception fırlatmaz, false döner.
/// </summary>
public sealed class SecurityEventParser(ILogger<SecurityEventParser> logger)
{
    private static readonly XNamespace EventNamespace = "http://schemas.microsoft.com/win/2004/08/events/event";

    private const string SystemElement = "System";
    private const string EventIdElement = "EventID";
    private const string TimeCreatedElement = "TimeCreated";
    private const string SystemTimeAttribute = "SystemTime";
    private const string EventDataElement = "EventData";
    private const string DataElement = "Data";
    private const string NameAttribute = "Name";
    private const string UsernameField = "TargetUserName";
    private const string IpAddressField = "IpAddress";

    /// <summary>Windows, IP'nin geçerli olmadığı oturumlarda bu değeri yazar.</summary>
    private const string NotApplicableValue = "-";

    public bool TryParse(string eventXml, string serverName, DateTimeOffset fallbackTimestamp, [NotNullWhen(true)] out SecurityEventDto? securityEvent)
    {
        securityEvent = null;

        try
        {
            var root = XDocument.Parse(eventXml).Root;
            var system = root?.Element(EventNamespace + SystemElement);

            if (system is null)
            {
                logger.LogWarning("Security event skipped: 'System' section missing.");
                return false;
            }

            if (!TryReadEventType(system, out var eventType))
            {
                return false;
            }

            var data = ReadEventData(root!);

            securityEvent = new SecurityEventDto(
                serverName,
                eventType,
                Truncate(Normalize(data.GetValueOrDefault(IpAddressField), SecurityEventConstraints.LocalSourceIp), SecurityEventConstraints.SourceIpMaxLength),
                Truncate(Normalize(data.GetValueOrDefault(UsernameField), SecurityEventConstraints.UnknownUsername), SecurityEventConstraints.UsernameMaxLength),
                ReadTimestamp(system, fallbackTimestamp));

            return true;
        }
        catch (Exception exception)
        {
            // Tek bir bozuk olay watcher'ı durdurmamalı; olay atlanır, akış devam eder.
            logger.LogWarning("Security event skipped: {Reason}: {Message}", exception.GetType().Name, exception.Message);
            return false;
        }
    }

    private bool TryReadEventType(XElement system, out SecurityEventType eventType)
    {
        eventType = default;
        var rawEventId = system.Element(EventNamespace + EventIdElement)?.Value;

        if (!int.TryParse(rawEventId, CultureInfo.InvariantCulture, out var eventId))
        {
            logger.LogWarning("Security event skipped: unreadable EventID '{EventId}'.", rawEventId);
            return false;
        }

        switch (eventId)
        {
            case WindowsSecurityEventIds.FailedLogin:
                eventType = SecurityEventType.FailedLogin;
                return true;
            case WindowsSecurityEventIds.SuccessfulLogin:
                eventType = SecurityEventType.SuccessfulLogin;
                return true;
            default:
                logger.LogWarning("Security event skipped: unexpected EventID {EventId}.", eventId);
                return false;
        }
    }

    private static Dictionary<string, string> ReadEventData(XElement root) =>
        root.Element(EventNamespace + EventDataElement)
            ?.Elements(EventNamespace + DataElement)
            .Where(element => element.Attribute(NameAttribute) is not null)
            .GroupBy(element => element.Attribute(NameAttribute)!.Value)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase)
        ?? [];

    private static DateTimeOffset ReadTimestamp(XElement system, DateTimeOffset fallback)
    {
        var rawTime = system.Element(EventNamespace + TimeCreatedElement)?.Attribute(SystemTimeAttribute)?.Value;

        return DateTimeOffset.TryParse(rawTime, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : fallback;
    }

    private static string Normalize(string? value, string valueWhenMissing) =>
        string.IsNullOrWhiteSpace(value) || value == NotApplicableValue ? valueWhenMissing : value.Trim();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
