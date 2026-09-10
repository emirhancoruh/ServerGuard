using Microsoft.Extensions.Options;
using ServerGuard.Agent.Configuration;
using ServerGuard.Agent.Transport;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// IIS'in W3C log dosyalarını takip eder, yalnızca yeni eklenen satırları okur ve backend'e gönderir.
/// Birden fazla site klasörü aynı anda izlenebilir.
/// </summary>
/// <remarks>
/// <para>
/// Her klasörün okuma konumu ayrı tutulur ve tek bir konum dosyasında birlikte saklanır.
/// Kuyruk tüm klasörler arasında paylaşıldığından bir turda klasörler <b>sırayla</b> işlenir:
/// bir klasörün kayıtları teslim edilmeden diğerine geçilmez. Bu kural olmadan, teslim
/// edilemeyen kayıtlar başka bir klasörün konumunun ilerlemesine yol açabilir ve o klasörün
/// satırları kaybolabilirdi.
/// </para>
/// </remarks>
public sealed class TrafficLogWorker(
    TrafficLogFileReader reader,
    W3CLogParser parser,
    LogOffsetStore offsetStore,
    LogDirectoryResolver directoryResolver,
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

    private readonly List<TrafficDirectoryTracker> _trackers = [];
    private readonly List<FileSystemWatcher> _watchers = [];

    private LogOffsetFile _offsets = new();

    /// <summary>Teslim edilemeyen kayıtları olan klasör; bir sonraki turda önce o denenir.</summary>
    private TrafficDirectoryTracker? _blockedTracker;

    private DateTimeOffset _lastDirectoryScanAt = DateTimeOffset.MinValue;

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

        var directories = directoryResolver.Resolve();

        if (directories.Count == 0)
        {
            // IIS kurulu değilse veya yol yanlışsa yalnızca bu toplayıcı durur; agent çalışmayı sürdürür.
            logger.LogWarning(
                "No IIS log directory found. Set Agent:Traffic:LogRoot (or LogDirectories) or " +
                "disable collection with Agent:Traffic:Enabled=false.");

            return;
        }

        _offsets = await offsetStore.LoadAsync(OffsetFilePath, directories[0], stoppingToken);
        await SynchronizeTrackersAsync(directories, stoppingToken);

        logger.LogInformation(
            "Traffic log watcher started. Server={ServerName} Directories={DirectoryCount} Pattern={Pattern}",
            _agentOptions.ServerName,
            _trackers.Count,
            _options.FilePattern);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Dosya değişince erken uyanır, değişmezse yoklama aralığında yine de kontrol eder:
                // FileSystemWatcher olayları kaçırabilir, tek başına güvenilmez.
                await _changeSignal.WaitAsync(_options.PollInterval, stoppingToken);

                await RescanDirectoriesIfDueAsync(stoppingToken);
                await ProcessTrackersAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal kapanış.
        }
        finally
        {
            StopWatchers();
        }

        logger.LogInformation("Traffic log watcher stopped. PendingInQueue={Count}", dispatcher.PendingCount);
    }

    public override void Dispose()
    {
        StopWatchers();
        _changeSignal.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Klasörleri sırayla işler. Bir klasör teslimat yapamadıysa tur orada biter; kalan
    /// klasörler bir sonraki turda ele alınır. Backend erişilemezken devam etmek yalnızca
    /// kuyruğu şişirirdi.
    /// </summary>
    private async Task ProcessTrackersAsync(CancellationToken cancellationToken)
    {
        foreach (var tracker in TrackersInResumeOrder())
        {
            var result = await tracker.ProcessAsync(PersistOffsetsAsync, cancellationToken);

            if (result == TrafficCycleResult.BackendUnavailable)
            {
                _blockedTracker = tracker;
                return;
            }
        }

        _blockedTracker = null;
    }

    /// <summary>
    /// Teslim edilemeyen kayıtları olan klasör listenin başına alınır; kuyruktaki kayıtların
    /// sahibi odur ve konumu ancak o klasör tarafından ilerletilebilir.
    /// </summary>
    private IEnumerable<TrafficDirectoryTracker> TrackersInResumeOrder()
    {
        if (_blockedTracker is not null)
        {
            yield return _blockedTracker;
        }

        foreach (var tracker in _trackers)
        {
            if (!ReferenceEquals(tracker, _blockedTracker))
            {
                yield return tracker;
            }
        }
    }

    /// <summary>Tüm klasörlerin konumlarını tek dosyaya yazar.</summary>
    private Task PersistOffsetsAsync(CancellationToken cancellationToken)
    {
        foreach (var tracker in _trackers)
        {
            if (tracker.CurrentOffset is { } offset)
            {
                _offsets.Sources[tracker.Directory] = offset;
            }
        }

        return offsetStore.SaveAsync(OffsetFilePath, _offsets, cancellationToken);
    }

    private async Task RescanDirectoriesIfDueAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        if (now - _lastDirectoryScanAt < _options.DirectoryRescanInterval)
        {
            return;
        }

        var directories = directoryResolver.Resolve();

        if (directories.Count > 0)
        {
            await SynchronizeTrackersAsync(directories, cancellationToken);
        }
    }

    /// <summary>
    /// İzlenen klasör listesini günceller: yeni klasörler için izleyici ve takipçi oluşturur.
    /// Kaybolan klasörler bırakılır ama konumları silinmez; geçici bir erişim sorunundan sonra
    /// klasör geri geldiğinde kaldığı yerden devam edilir.
    /// </summary>
    private async Task SynchronizeTrackersAsync(
        IReadOnlyList<string> directories,
        CancellationToken cancellationToken)
    {
        _lastDirectoryScanAt = timeProvider.GetUtcNow();

        foreach (var directory in directories)
        {
            if (_trackers.Any(tracker =>
                    string.Equals(tracker.Directory, directory, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var tracker = new TrafficDirectoryTracker(
                directory,
                reader,
                parser,
                dispatcher,
                _options,
                _agentOptions.ServerName,
                timeProvider,
                logger);

            await tracker.InitializeAsync(_offsets.Sources.GetValueOrDefault(directory), cancellationToken);

            _trackers.Add(tracker);
            StartWatcher(directory);

            logger.LogInformation("Watching IIS log directory. Directory={Directory}", directory);
        }
    }

    private void StartWatcher(string directory)
    {
        try
        {
            var watcher = new FileSystemWatcher(directory, _options.FilePattern)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            watcher.Changed += OnLogDirectoryChanged;
            watcher.Created += OnLogDirectoryChanged;
            watcher.Renamed += OnLogDirectoryChanged;
            watcher.Error += OnWatcherError;

            _watchers.Add(watcher);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // İzleyici kurulamazsa yoklama aralığı yine de veriyi toplar; toplayıcı durmaz.
            logger.LogWarning(
                "File watcher could not be started ({Reason}: {Message}); polling continues. Directory={Directory}",
                exception.GetType().Name,
                exception.Message,
                directory);
        }
    }

    private void StopWatchers()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Changed -= OnLogDirectoryChanged;
            watcher.Created -= OnLogDirectoryChanged;
            watcher.Renamed -= OnLogDirectoryChanged;
            watcher.Error -= OnWatcherError;
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
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
