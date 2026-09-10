using Microsoft.Extensions.Options;

namespace ServerGuard.Agent.Traffic;

/// <summary>
/// Ayarlardan izlenecek log klasörlerinin listesini çıkarır.
/// </summary>
/// <remarks>
/// Klasör keşfi ayrı bir sınıfta tutulur; böylece toplayıcı "hangi klasörler" sorusuyla değil,
/// yalnızca "bu klasörü nasıl okurum" sorusuyla ilgilenir.
/// </remarks>
public sealed class LogDirectoryResolver(
    IOptions<TrafficOptions> options,
    ILogger<LogDirectoryResolver> logger)
{
    private readonly TrafficOptions _options = options.Value;

    /// <summary>
    /// Var olan klasörleri sıralı ve tekrarsız biçimde döner. Ayarda belirtilip de
    /// bulunamayan klasörler uyarı ile atlanır; biri yok diye diğerleri izlenmemezlik etmez.
    /// </summary>
    public IReadOnlyList<string> Resolve()
    {
        var candidates = string.IsNullOrWhiteSpace(_options.LogRoot)
            ? ConfiguredDirectories()
            : DiscoverUnderRoot();

        var existing = candidates
            .Where(DirectoryExists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(directory => directory, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (existing.Length <= _options.MaxTrackedDirectories)
        {
            return existing;
        }

        logger.LogWarning(
            "Found {FoundCount} log directories but only {Limit} will be watched " +
            "(Agent:Traffic:MaxTrackedDirectories). Check that Agent:Traffic:LogRoot points at the IIS log root.",
            existing.Length,
            _options.MaxTrackedDirectories);

        return existing[.._options.MaxTrackedDirectories];
    }

    private IEnumerable<string> ConfiguredDirectories() =>
        _options.LogDirectories.Count > 0
            ? _options.LogDirectories
            : [_options.LogDirectory];

    private IEnumerable<string> DiscoverUnderRoot()
    {
        if (!Directory.Exists(_options.LogRoot))
        {
            logger.LogWarning(
                "IIS log root not found: {LogRoot}. Set Agent:Traffic:LogRoot or use Agent:Traffic:LogDirectories.",
                _options.LogRoot);

            return [];
        }

        try
        {
            return Directory.EnumerateDirectories(_options.LogRoot, _options.DirectoryPattern);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                "IIS log root could not be listed ({Reason}: {Message}); will retry. LogRoot={LogRoot}",
                exception.GetType().Name,
                exception.Message,
                _options.LogRoot);

            return [];
        }
    }

    private bool DirectoryExists(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        if (Directory.Exists(directory))
        {
            return true;
        }

        logger.LogWarning("Configured log directory not found; skipped. Directory={Directory}", directory);
        return false;
    }
}
