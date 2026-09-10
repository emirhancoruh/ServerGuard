using ServerGuard.Agent.Transport;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// Tek bir IIS site klasörünü takip eder: en yeni log dosyasını bulur, yalnızca yeni satırları
/// okur, ayrıştırır ve backend'e gönderilmek üzere kuyruğa alır.
/// </summary>
/// <remarks>
/// <para>
/// Okuma konumu ancak satırlar backend'e <b>ulaştıktan sonra</b> kalıcı hale getirilir.
/// Backend erişilemezken konum ilerlemez ve yeni satır okunmaz; log dosyasının kendisi
/// tampon görevi görür. Böylece veri kaybı olmaz. Süreç tam teslimat ile konumun yazılması
/// arasında düşerse birkaç satır tekrar okunabilir (en az bir kez teslim).
/// </para>
/// <para>
/// Kuyruk tüm klasörler arasında paylaşıldığından, bir turda yalnızca tek bir klasör
/// okuma yapar. Bu kural <see cref="TrafficLogWorker"/> tarafından uygulanır; burada
/// yalnızca "kuyruk boşalmadan konum ilerlemez" değişmezi korunur.
/// </para>
/// </remarks>
public sealed class TrafficDirectoryTracker(
    string directory,
    TrafficLogFileReader reader,
    W3CLogParser parser,
    BackendDispatcher<TrafficLogDto> dispatcher,
    TrafficOptions options,
    string serverName,
    TimeProvider timeProvider,
    ILogger logger)
{
    private W3CFieldMap? _fieldMap;
    private string? _currentFileName;
    private long _committedOffset;
    private long _pendingOffset;
    private bool _missingFieldMapReported;

    /// <summary>İzlenen klasörün tam yolu; konum dosyasındaki anahtardır.</summary>
    public string Directory { get; } = directory;

    /// <summary>Kalıcı hale getirilecek güncel konum. Henüz dosya izlenmiyorsa <c>null</c>.</summary>
    public LogOffset? CurrentOffset =>
        _currentFileName is null ? null : new LogOffset(_currentFileName, _committedOffset);

    /// <summary>
    /// Saklanmış konumdan devam etmeye hazırlanır. Kayıtlı dosya artık yoksa konum
    /// yok sayılır ve ilk turda en yeni dosyadan başlanır.
    /// </summary>
    public async Task InitializeAsync(LogOffset? storedOffset, CancellationToken cancellationToken)
    {
        if (storedOffset is null)
        {
            return;
        }

        var path = Path.Combine(Directory, storedOffset.FileName);

        if (!File.Exists(path))
        {
            logger.LogInformation(
                "Stored log file no longer exists; starting from the newest file. Directory={Directory} File={File}",
                Directory,
                storedOffset.FileName);

            return;
        }

        _currentFileName = storedOffset.FileName;
        _committedOffset = storedOffset.Offset;
        _pendingOffset = storedOffset.Offset;
        _fieldMap = await reader.ReadFieldMapAsync(path, cancellationToken);

        logger.LogInformation(
            "Resuming traffic log from stored offset. Directory={Directory} File={File} Offset={Offset}",
            Directory,
            storedOffset.FileName,
            storedOffset.Offset);
    }

    /// <summary>
    /// Bir okuma turu çalıştırır. <paramref name="persistOffsetsAsync"/> her başarılı
    /// teslimattan sonra çağrılır; konumun kalıcı hale getirilmesi çağırana aittir çünkü
    /// dosya tüm klasörlerin konumlarını birlikte tutar.
    /// </summary>
    public async Task<TrafficCycleResult> ProcessAsync(
        Func<CancellationToken, Task> persistOffsetsAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            // Önceki turdan teslim edilmemiş kayıt varsa önce onlar gönderilir;
            // teslim edilmeden yeni satır okunmaz, böylece kuyrukta mükerrer kayıt oluşmaz.
            if (dispatcher.PendingCount > 0)
            {
                return await FlushAndCommitAsync(persistOffsetsAsync, cancellationToken);
            }

            var newestFileName = FindNewestFileName();

            if (newestFileName is null)
            {
                return TrafficCycleResult.Completed;
            }

            if (_currentFileName is null)
            {
                await TrackFileAsync(
                    newestFileName,
                    options.ReadExistingFileOnFirstRun,
                    persistOffsetsAsync,
                    cancellationToken);
            }

            var (linesRead, result) = await ReadAndDispatchAsync(persistOffsetsAsync, cancellationToken);

            if (result == TrafficCycleResult.BackendUnavailable)
            {
                return result;
            }

            // İzlenen dosyada okunacak satır kalmadıysa ve daha yeni bir dosya oluştuysa geçiş yapılır.
            // Önce eski dosyanın sonu okunur; böylece devir sırasında son satırlar kaybolmaz.
            if (linesRead == 0 && _currentFileName != newestFileName && dispatcher.PendingCount == 0)
            {
                logger.LogInformation(
                    "Log file rotated. Directory={Directory} Previous={Previous} Current={Current}",
                    Directory,
                    _currentFileName,
                    newestFileName);

                await TrackFileAsync(newestFileName, fromBeginning: true, persistOffsetsAsync, cancellationToken);
            }

            return TrafficCycleResult.Completed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Tek bir klasörün hatası diğerlerini ve worker'ı durdurmamalı.
            logger.LogError(
                exception,
                "Traffic log cycle failed for {Directory}; will retry on next tick.",
                Directory);

            return TrafficCycleResult.Completed;
        }
    }

    private async Task<(int LinesRead, TrafficCycleResult Result)> ReadAndDispatchAsync(
        Func<CancellationToken, Task> persistOffsetsAsync,
        CancellationToken cancellationToken)
    {
        var path = CurrentFilePath();
        var totalLines = 0;

        while (totalLines < options.MaxLinesPerCycle && !cancellationToken.IsCancellationRequested)
        {
            var chunk = await reader.ReadLinesAsync(path, _committedOffset, cancellationToken);

            if (!chunk.HasLines)
            {
                // Dosya kesilmişse okuyucu konumu sıfırlar; bunu kalıcı hale getir.
                if (chunk.NextOffset != _committedOffset)
                {
                    _committedOffset = chunk.NextOffset;
                    _pendingOffset = chunk.NextOffset;
                    await persistOffsetsAsync(cancellationToken);
                }

                break;
            }

            var enqueued = EnqueueLines(chunk.Lines);
            _pendingOffset = chunk.NextOffset;
            totalLines += chunk.Lines.Count;

            if (enqueued == 0)
            {
                // Yalnızca başlık/yorum satırları vardı; gönderilecek bir şey yok, konum ilerler.
                _committedOffset = _pendingOffset;
                await persistOffsetsAsync(cancellationToken);
                continue;
            }

            if (await FlushAndCommitAsync(persistOffsetsAsync, cancellationToken) ==
                TrafficCycleResult.BackendUnavailable)
            {
                return (totalLines, TrafficCycleResult.BackendUnavailable);
            }
        }

        return (totalLines, TrafficCycleResult.Completed);
    }

    /// <summary>
    /// Kuyruğu boşaltır ve <b>yalnızca tamamı teslim edildiyse</b> okuma konumunu ilerletir.
    /// </summary>
    private async Task<TrafficCycleResult> FlushAndCommitAsync(
        Func<CancellationToken, Task> persistOffsetsAsync,
        CancellationToken cancellationToken)
    {
        await dispatcher.FlushAsync(cancellationToken);

        if (dispatcher.PendingCount > 0)
        {
            logger.LogWarning(
                "Traffic offset not advanced; {Count} record(s) still undelivered. Directory={Directory}",
                dispatcher.PendingCount,
                Directory);

            return TrafficCycleResult.BackendUnavailable;
        }

        _committedOffset = _pendingOffset;
        await persistOffsetsAsync(cancellationToken);

        return TrafficCycleResult.Completed;
    }

    private int EnqueueLines(IReadOnlyList<string> lines)
    {
        var enqueued = 0;
        var now = timeProvider.GetUtcNow();

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (line[0] == W3CFieldMap.CommentPrefix)
            {
                ApplyDirective(line);
                continue;
            }

            if (_fieldMap is null)
            {
                ReportMissingFieldMapOnce();
                continue;
            }

            if (parser.TryParse(line, _fieldMap, serverName, now, out var trafficLog))
            {
                dispatcher.Enqueue(trafficLog);
                enqueued++;
            }
        }

        return enqueued;
    }

    /// <summary>
    /// IIS log yapılandırması değişirse dosyanın ortasında yeni bir "#Fields:" satırı yazar;
    /// alan sırası buradan güncellenir.
    /// </summary>
    private void ApplyDirective(string line)
    {
        var updated = W3CFieldMap.TryCreate(line);

        if (updated is null)
        {
            return;
        }

        _fieldMap = updated;
        _missingFieldMapReported = false;

        logger.LogInformation(
            "W3C field map updated from log header. Directory={Directory} FieldCount={FieldCount}",
            Directory,
            updated.FieldCount);
    }

    private void ReportMissingFieldMapOnce()
    {
        if (_missingFieldMapReported)
        {
            return;
        }

        _missingFieldMapReported = true;

        logger.LogWarning(
            "Traffic lines skipped: no '#Fields:' directive found yet. Directory={Directory} File={File}",
            Directory,
            _currentFileName);
    }

    private async Task TrackFileAsync(
        string fileName,
        bool fromBeginning,
        Func<CancellationToken, Task> persistOffsetsAsync,
        CancellationToken cancellationToken)
    {
        _currentFileName = fileName;
        _missingFieldMapReported = false;

        var path = CurrentFilePath();
        _fieldMap = await reader.ReadFieldMapAsync(path, cancellationToken);

        _committedOffset = fromBeginning ? 0 : SafeFileLength(path);
        _pendingOffset = _committedOffset;

        await persistOffsetsAsync(cancellationToken);

        logger.LogInformation(
            "Tracking log file. Directory={Directory} File={File} StartOffset={Offset} FieldsKnown={FieldsKnown}",
            Directory,
            fileName,
            _committedOffset,
            _fieldMap is not null);
    }

    private string CurrentFilePath() => Path.Combine(Directory, _currentFileName!);

    /// <summary>
    /// Klasördeki en yeni log dosyasını bulur. IIS dosyaları tarih içeren adlarla oluşturduğundan
    /// ada göre sıralamak, sistem saati değişse bile doğru sonucu verir.
    /// </summary>
    private string? FindNewestFileName()
    {
        try
        {
            return System.IO.Directory
                .EnumerateFiles(Directory, options.FilePattern)
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                "Log directory could not be listed ({Reason}: {Message}); will retry. Directory={Directory}",
                exception.GetType().Name,
                exception.Message,
                Directory);

            return null;
        }
    }

    private static long SafeFileLength(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? info.Length : 0;
    }
}
