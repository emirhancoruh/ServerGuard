using System.Text.Json;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// Okuma konumlarını küçük bir JSON dosyasında saklar; agent yeniden başladığında
/// her klasör için kaldığı yerden devam edebilsin diye.
/// </summary>
public sealed class LogOffsetStore(ILogger<LogOffsetStore> logger)
{
    private const string TempFileSuffix = ".tmp";
    private const string LegacyFileNameProperty = "fileName";
    private const string LegacyOffsetProperty = "offset";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Konumları okur. Dosya tek klasörlü eski biçimdeyse dönüştürülür; böylece sürüm
    /// yükseltmesinden sonra izlenen dosya baştan okunmaz ve mükerrer kayıt oluşmaz.
    /// </summary>
    /// <param name="legacyDirectory">
    /// Eski biçimdeki konumun atanacağı klasör; genellikle ilk izlenen klasördür.
    /// </param>
    public async Task<LogOffsetFile> LoadAsync(
        string path,
        string? legacyDirectory,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new LogOffsetFile();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);

            return TryReadCurrentFormat(json)
                ?? TryReadLegacyFormat(json, legacyDirectory)
                ?? new LogOffsetFile();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Bozuk konum dosyası okumayı engellememeli; baştan başlanır.
            logger.LogWarning(
                "Offset file could not be read ({Reason}: {Message}); starting fresh. File={File}",
                exception.GetType().Name,
                exception.Message,
                path);

            return new LogOffsetFile();
        }
    }

    /// <summary>
    /// Konumları önce geçici dosyaya yazıp sonra taşır. Yazma sırasında süreç düşerse
    /// mevcut konum dosyası bozulmadan kalır.
    /// </summary>
    public async Task SaveAsync(string path, LogOffsetFile offsets, CancellationToken cancellationToken)
    {
        var temporaryPath = path + TempFileSuffix;

        try
        {
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, offsets, SerializerOptions, cancellationToken);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Konum yazılamazsa okuma sürer; en kötü ihtimalle yeniden başlatmada
            // birkaç satır tekrar okunur. Bu, worker'ı düşürmekten iyidir.
            logger.LogError(
                "Offset could not be saved ({Reason}: {Message}). File={File}",
                exception.GetType().Name,
                exception.Message,
                path);
        }
    }

    private static LogOffsetFile? TryReadCurrentFormat(string json)
    {
        var parsed = JsonSerializer.Deserialize<LogOffsetFile>(json, SerializerOptions);

        return parsed?.Sources.Count > 0 ? parsed : null;
    }

    private LogOffsetFile? TryReadLegacyFormat(string json, string? legacyDirectory)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetProperty(document.RootElement, LegacyFileNameProperty, out var fileNameElement) ||
            !TryGetProperty(document.RootElement, LegacyOffsetProperty, out var offsetElement))
        {
            return null;
        }

        var fileName = fileNameElement.GetString();

        if (string.IsNullOrWhiteSpace(fileName) ||
            !offsetElement.TryGetInt64(out var offset) ||
            string.IsNullOrWhiteSpace(legacyDirectory))
        {
            return null;
        }

        logger.LogInformation(
            "Migrated single-directory offset file to the multi-directory format. Directory={Directory} File={File} Offset={Offset}",
            legacyDirectory,
            fileName,
            offset);

        var migrated = new LogOffsetFile();
        migrated.Sources[legacyDirectory] = new LogOffset(fileName, offset);

        return migrated;
    }

    /// <summary>JSON alan adları büyük/küçük harf farkıyla yazılmış olabilir.</summary>
    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
