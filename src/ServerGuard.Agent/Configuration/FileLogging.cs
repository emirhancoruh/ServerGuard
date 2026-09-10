using Serilog;
using Serilog.Configuration;

namespace ServerGuard.Agent.Configuration;

/// <summary>
/// Agent'ın dosyaya log yazımını yapılandırır.
/// </summary>
/// <remarks>
/// <para>
/// Yol, agent'ın kurulu olduğu klasöre göre <b>mutlak</b> hesaplanır. Windows hizmeti olarak
/// çalışan bir sürecin çalışma dizini <c>C:\Windows\System32</c> olabilir; göreli bir yol
/// log dosyalarını oraya düşürürdü.
/// </para>
/// <para>
/// Böylece sorun yaşandığında sunucuya bağlanıp Olay Görüntüleyici'de arama yapmak yerine
/// agent klasöründeki <c>logs</c> altına bakmak yeterlidir.
/// </para>
/// </remarks>
public static class FileLogging
{
    public const string FolderName = "logs";

    private const string FileNamePrefix = "agent-.log";
    private const int RetainedFileCount = 14;
    private const long FileSizeLimitBytes = 20L * 1024 * 1024;

    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}";

    public static LoggerConfiguration WriteToRollingFile(this LoggerSinkConfiguration sink, string contentRootPath) =>
        sink.File(
            path: ResolvePath(contentRootPath),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: RetainedFileCount,
            fileSizeLimitBytes: FileSizeLimitBytes,
            rollOnFileSizeLimit: true,
            shared: true,
            outputTemplate: OutputTemplate);

    public static string ResolvePath(string contentRootPath) =>
        Path.Combine(contentRootPath, FolderName, FileNamePrefix);
}
