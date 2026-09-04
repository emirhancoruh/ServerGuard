using Microsoft.Extensions.Options;
using ServerGuard.Agent.Configuration;
using ServerGuard.Agent.Transport;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// IIS'in W3C log dosyasını takip eder, yalnızca yeni eklenen satırları okur ve backend'e gönderir.
/// </summary>
/// <remarks>
/// Okuma konumu ancak satırlar backend'e <b>ulaştıktan sonra</b> kalıcı hale getirilir.
/// Backend erişilemezken konum ilerlemez ve yeni satır okunmaz; log dosyasının kendisi
/// tampon görevi görür. Böylece veri kaybı olmaz. Süreç tam teslimat ile konumun yazılması
/// arasında düşerse birkaç satır tekrar okunabilir (en az bir kez teslim).
/// </remarks>
public sealed class TrafficLogWorker(
    TrafficLogFileReader reader,
    W3CLogParser parser,
    LogOffsetStore offsetStore,
    BackendDispatcher<TrafficLogDto> dispatcher,
    IOptions<AgentOptions> agentOptions,
    IOptions<TrafficOptions> trafficOptions,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<TrafficLogWorker> logger) : BackgroundService
{
    private readonly AgentOptions _agentOptions = agentOptions.Value;
    private readonly TrafficOptions _options = trafficOptions.Value;

    /// <summary>Dosya değişikliğinde döngüyü erken uyandırır.</summary>
    private readonly SemaphoreSlim _changeSignal = new(0, 1);

    private FileSystemWatcher? _watcher;
    private W3CFieldMap? _fieldMap;
    private string? _currentFileName;
    private long _committedOffset;
    private long _pendingOffset;
    private bool _missingFieldMapReported;

    private string OffsetFilePath => Path.IsPathRooted(_options.OffsetFilePath)
        ? _options.OffsetFilePath
        : Path.Combine(environment.ContentRootPath, _options.OffsetFilePath);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Traffic log collection is disabled by configuration.");
            return;
        }

        if (!Directory.Exists(_options.LogDirectory))
        {
            // IIS kurulu değilse veya yol yanlışsa yalnızca bu toplayıcı durur; agent çalışmayı sürdürür.
            logger.LogWarning(
                "IIS log directory not found: {Directory}. Set Agent:Traffic:LogDirectory or " +
                "disable it with Agent:Traffic:Enabled=false.",
                _options.LogDirectory);

            return;
        }

        await RestoreOffsetAsync(stoppingToken);
        StartWatcher();

        logger.LogInformation(
            "Traffic log watcher started. Server={ServerName} Directory={Directory} Pattern={Pattern}",
            _agentOptions.ServerName,
            _options.LogDirectory,
            _options.FilePattern);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Dosya değişince erken uyanır, değişmezse yoklama aralığında yine de kontrol eder:
                // FileSystemWatcher olayları kaçırabilir, tek başına güvenilmez.
                await _changeSignal.WaitAsync(_options.PollInterval, stoppingToken);
                await ProcessAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }
        finally
        {
            StopWatcher();
        }

        logger.LogInformation("Traffic log watcher stopped. PendingInQueue={Count}", dispatcher.PendingCount);
    }

    public override void Dispose()
    {
        StopWatcher();
        _changeSignal.Dispose();
        base.Dispose();
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Önceki turdan teslim edilmemiş kayıt varsa önce onlar gönderilir;
            // teslim edilmeden yeni satır okunmaz, böylece kuyrukta mükerrer kayıt oluşmaz.
            if (dispatcher.PendingCount > 0)
            {
                await FlushAndCommitAsync(cancellationToken);
                return;
            }

            var newestFileName = FindNewestFileName();

            if (newestFileName is null)
            {
                return;
            }

            if (_currentFileName is null)
            {
                await TrackFileAsync(newestFileName, _options.ReadExistingFileOnFirstRun, cancellationToken);
            }

            var linesRead = await ReadAndDispatchAsync(cancellationToken);

            // İzlenen dosyada okunacak satır kalmadıysa ve daha yeni bir dosya oluştuysa geçiş yapılır.
            // Önce eski dosyanın sonu okunur; böylece devir sırasında son satırlar kaybolmaz.
            if (linesRead == 0 && _currentFileName != newestFileName && dispatcher.PendingCount == 0)
            {
                logger.LogInformation(
                    "Log file rotated. Previous={Previous} Current={Current}",
                    _currentFileName,
                    newestFileName);

                await TrackFileAsync(newestFileName, fromBeginning: true, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Tek bir turun hatası worker'ı düşürmemeli; bir sonraki turda devam edilir.
            logger.LogError(exception, "Traffic log cycle failed; will retry on next tick.");
        }
    }

    private async Task<int> ReadAndDispatchAsync(CancellationToken cancellationToken)
    {
        var path = CurrentFilePath();
        var totalLines = 0;

        while (totalLines < _options.MaxLinesPerCycle && !cancellationToken.IsCancellationRequested)
        {
            var chunk = await reader.ReadLinesAsync(path, _committedOffset, cancellationToken);

            if (!chunk.HasLines)
            {
                // Dosya kesilmişse okuyucu konumu sıfırlar; bunu kalıcı hale getir.
                if (chunk.NextOffset != _committedOffset)
                {
                    _committedOffset = chunk.NextOffset;
                    _pendingOffset = chunk.NextOffset;
                    await SaveOffsetAsync(cancellationToken);
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
                await SaveOffsetAsync(cancellationToken);
                continue;
            }

            await FlushAndCommitAsync(cancellationToken);

            if (dispatcher.PendingCount > 0)
            {
                // Backend erişilemiyor; okumayı durdur, konum ilerlemesin.
                break;
            }
        }

        return totalLines;
    }

    /// <summary>
    /// Kuyruğu boşaltır ve <b>yalnızca tamamı teslim edildiyse</b> okuma konumunu ilerletir.
    /// </summary>
    private async Task FlushAndCommitAsync(CancellationToken cancellationToken)
    {
        await dispatcher.FlushAsync(cancellationToken);

        if (dispatcher.PendingCount > 0)
        {
            logger.LogWarning(
                "Traffic offset not advanced; {Count} record(s) still undelivered.",
                dispatcher.PendingCount);

            return;
        }

        _committedOffset = _pendingOffset;
        await SaveOffsetAsync(cancellationToken);
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

            if (parser.TryParse(line, _fieldMap, _agentOptions.ServerName, now, out var trafficLog))
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
        logger.LogInformation("W3C field map updated from log header. FieldCount={FieldCount}", updated.FieldCount);
    }

    private void ReportMissingFieldMapOnce()
    {
        if (_missingFieldMapReported)
        {
            return;
        }

        _missingFieldMapReported = true;
        logger.LogWarning(
            "Traffic lines skipped: no '#Fields:' directive found yet. File={File}",
            _currentFileName);
    }

    private async Task TrackFileAsync(string fileName, bool fromBeginning, CancellationToken cancellationToken)
    {
        _currentFileName = fileName;
        _missingFieldMapReported = false;

        var path = CurrentFilePath();
        _fieldMap = await reader.ReadFieldMapAsync(path, cancellationToken);

        _committedOffset = fromBeginning ? 0 : SafeFileLength(path);
        _pendingOffset = _committedOffset;

        await SaveOffsetAsync(cancellationToken);

        logger.LogInformation(
            "Tracking log file. File={File} StartOffset={Offset} FieldsKnown={FieldsKnown}",
            fileName,
            _committedOffset,
            _fieldMap is not null);
    }

    private async Task RestoreOffsetAsync(CancellationToken cancellationToken)
    {
        var stored = await offsetStore.LoadAsync(OffsetFilePath, cancellationToken);

        if (stored is null)
        {
            return;
        }

        var path = Path.Combine(_options.LogDirectory, stored.FileName);

        if (!File.Exists(path))
        {
            logger.LogInformation(
                "Stored log file no longer exists; starting from the newest file. File={File}",
                stored.FileName);

            return;
        }

        _currentFileName = stored.FileName;
        _committedOffset = stored.Offset;
        _pendingOffset = stored.Offset;
        _fieldMap = await reader.ReadFieldMapAsync(path, cancellationToken);

        logger.LogInformation(
            "Resuming traffic log from stored offset. File={File} Offset={Offset}",
            stored.FileName,
            stored.Offset);
    }

    private Task SaveOffsetAsync(CancellationToken cancellationToken) =>
        _currentFileName is null
            ? Task.CompletedTask
            : offsetStore.SaveAsync(OffsetFilePath, new LogOffset(_currentFileName, _committedOffset), cancellationToken);

    private string CurrentFilePath() => Path.Combine(_options.LogDirectory, _currentFileName!);

    /// <summary>
    /// Klasördeki en yeni log dosyasını bulur. IIS dosyaları tarih içeren adlarla oluşturduğundan
    /// ada göre sıralamak, sistem saati değişse bile doğru sonucu verir.
    /// </summary>
    private string? FindNewestFileName()
    {
        try
        {
            return Directory
                .EnumerateFiles(_options.LogDirectory, _options.FilePattern)
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
                _options.LogDirectory);

            return null;
        }
    }

    private static long SafeFileLength(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? info.Length : 0;
    }

    private void StartWatcher()
    {
        _watcher = new FileSystemWatcher(_options.LogDirectory, _options.FilePattern)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnLogDirectoryChanged;
        _watcher.Created += OnLogDirectoryChanged;
        _watcher.Renamed += OnLogDirectoryChanged;
        _watcher.Error += OnWatcherError;
    }

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.Changed -= OnLogDirectoryChanged;
        _watcher.Created -= OnLogDirectoryChanged;
        _watcher.Renamed -= OnLogDirectoryChanged;
        _watcher.Error -= OnWatcherError;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    /// <summary>
    /// Watcher'ın kendi thread'inde çağrılır; yalnızca döngüyü uyandırır, iş yapmaz.
    /// </summary>
    private void OnLogDirectoryChanged(object sender, FileSystemEventArgs args) => SignalChange();

    private void OnWatcherError(object sender, ErrorEventArgs args) =>
        // Yoklama aralığı devrede olduğundan izleme kesilse de veri akışı durmaz.
        logger.LogWarning(
            "File watcher reported an error ({Message}); polling continues.",
            args.GetException().Message);

    private void SignalChange()
    {
        if (_changeSignal.CurrentCount > 0)
        {
            return;
        }

        try
        {
            _changeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Aynı anda birden fazla olay geldi; bir uyandırma yeterli.
        }
        catch (ObjectDisposedException)
        {
            // Kapanış sırasında gelen olay; yok sayılır.
        }
    }
}
