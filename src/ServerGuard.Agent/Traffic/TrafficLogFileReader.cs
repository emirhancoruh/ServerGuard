using System.Text;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// IIS log dosyasını, IIS yazmaya devam ederken okur.
/// </summary>
/// <remarks>
/// Dosya IIS tarafından açık tutulduğundan <see cref="FileShare.ReadWrite"/> ile açılır;
/// döndürme sırasında yeniden adlandırma/silme olabileceği için <see cref="FileShare.Delete"/> de eklenir.
/// </remarks>
public sealed class TrafficLogFileReader(ILogger<TrafficLogFileReader> logger)
{
    /// <summary>Tek seferde okunacak en fazla bayt.</summary>
    private const int ReadBufferBytes = 64 * 1024;

    /// <summary>Başlıktaki "#Fields:" yönergesinin aranacağı azami bayt.</summary>
    private const int HeaderScanBytes = 64 * 1024;

    private const byte LineFeed = (byte)'\n';

    /// <summary>
    /// Verilen konumdan itibaren yalnızca <b>tamamlanmış</b> satırları okur.
    /// Yarım yazılmış son satır okunmaz ve konum onun başında bırakılır; böylece
    /// bir sonraki turda satır tamamlandığında baştan ve eksiksiz okunur.
    /// </summary>
    public async Task<LogChunk> ReadLinesAsync(string path, long offset, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = OpenShared(path);

            if (offset > stream.Length)
            {
                // Dosya kısalmış (kesilmiş veya aynı adla yeniden oluşturulmuş): baştan okunur.
                logger.LogWarning(
                    "Log file is shorter than the stored offset; restarting from the beginning. File={File} Offset={Offset} Length={Length}",
                    path,
                    offset,
                    stream.Length);

                offset = 0;
            }

            if (offset == stream.Length)
            {
                return LogChunk.Empty(offset);
            }

            stream.Seek(offset, SeekOrigin.Begin);

            var buffer = new byte[ReadBufferBytes];
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);

            if (read == 0)
            {
                return LogChunk.Empty(offset);
            }

            var lastLineFeed = Array.LastIndexOf(buffer, LineFeed, read - 1);

            if (lastLineFeed < 0)
            {
                // Henüz tek bir satır bile tamamlanmamış.
                return LogChunk.Empty(offset);
            }

            // Satır sonunda bölmek UTF-8 için güvenlidir: devam baytları hiçbir zaman 0x0A olmaz.
            var text = Encoding.UTF8.GetString(buffer, 0, lastLineFeed + 1);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new LogChunk(lines, offset + lastLineFeed + 1);
        }
        catch (Exception exception) when (IsTransientFileError(exception))
        {
            // Dosya kilitli veya erişilemiyor; bir sonraki turda tekrar denenir.
            logger.LogWarning(
                "Log file could not be read ({Reason}: {Message}); will retry. File={File}",
                exception.GetType().Name,
                exception.Message,
                path);

            return LogChunk.Empty(offset);
        }
    }

    /// <summary>
    /// Dosyanın başındaki son "#Fields:" yönergesini okur. Konumdan devam edildiğinde
    /// alan sırası bilinmediği için gereklidir.
    /// </summary>
    public async Task<W3CFieldMap?> ReadFieldMapAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = OpenShared(path);

            var buffer = new byte[(int)Math.Min(HeaderScanBytes, stream.Length)];
            var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);

            if (read == 0)
            {
                return null;
            }

            var text = Encoding.UTF8.GetString(buffer, 0, read);
            W3CFieldMap? fieldMap = null;

            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                // Başlıkta birden fazla yönerge varsa sonuncusu geçerlidir.
                fieldMap = W3CFieldMap.TryCreate(line) ?? fieldMap;
            }

            return fieldMap;
        }
        catch (Exception exception) when (IsTransientFileError(exception))
        {
            logger.LogWarning(
                "Log header could not be read ({Reason}: {Message}); will retry. File={File}",
                exception.GetType().Name,
                exception.Message,
                path);

            return null;
        }
    }

    private static FileStream OpenShared(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    private static bool IsTransientFileError(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;
}
