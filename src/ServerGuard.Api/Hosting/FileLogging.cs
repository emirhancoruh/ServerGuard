using Serilog;
using Serilog.Configuration;

namespace ServerGuard.Api.Hosting;

/// <summary>
/// Dosyaya log yazımını yapılandırır.
/// </summary>
/// <remarks>
/// <para>
/// Yol koda gömülü olarak <b>mutlak</b> hesaplanır. Serilog göreli yolları sürecin çalışma
/// dizinine göre çözer; IIS altında bu dizin uygulamanın klasörü olmayabilir ve log dosyaları
/// beklenmedik bir yere düşer. İzleme aracının kendi log'unun nerede olduğu belirsiz olamaz.
/// </para>
/// <para>
/// IIS uygulama havuzu kimliğinin bu klasöre yazma yetkisi olmalıdır; kurulum adımları
/// README'de belirtilmiştir.
/// </para>
/// </remarks>
public static class FileLogging
{
    public const string FolderName = "logs";

    private const string FileNamePrefix = "api-.log";
    private const int RetainedFileCount = 30;
    private const long FileSizeLimitBytes = 50L * 1024 * 1024;

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
