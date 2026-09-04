using System.Text.Json;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// Okuma konumunu küçük bir JSON dosyasında saklar; agent yeniden başladığında
/// kaldığı yerden devam edebilsin diye.
/// </summary>
public sealed class LogOffsetStore(ILogger<LogOffsetStore> logger)
{
    private const string TempFileSuffix = ".tmp";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public async Task<LogOffset?> LoadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<LogOffset>(stream, SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Bozuk konum dosyası okumayı engellememeli; baştan başlanır.
            logger.LogWarning(
                "Offset file could not be read ({Reason}: {Message}); starting fresh. File={File}",
                exception.GetType().Name,
                exception.Message,
                path);

            return null;
        }
    }

    /// <summary>
    /// Konumu önce geçici dosyaya yazıp sonra taşır. Yazma sırasında süreç düşerse
    /// mevcut konum dosyası bozulmadan kalır.
    /// </summary>
    public async Task SaveAsync(string path, LogOffset offset, CancellationToken cancellationToken)
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
                await JsonSerializer.SerializeAsync(stream, offset, SerializerOptions, cancellationToken);
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
}
