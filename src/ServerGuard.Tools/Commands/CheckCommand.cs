using ServerGuard.Tools.CommandLine;
using ServerGuard.Tools.Probing;
using ServerGuard.Tools.Reporting;

namespace ServerGuard.Tools.Commands;

/// <summary>
/// Çalışan bir API'ye dışarıdan istek atarak durumunu raporlar.
/// </summary>
/// <remarks>
/// Görev Zamanlayıcı'dan periyodik çalıştırmaya uygundur: başarısız kontrol varsa
/// çıkış kodu 1 olur, böylece zamanlanmış görev başarısız olarak işaretlenir.
/// </remarks>
public sealed class CheckCommand : ICommand
{
    private const string UrlOption = "url";
    private const string UserOption = "user";
    private const string PasswordOption = "password";
    private const string IngestKeyOption = "ingest-key";
    private const string InsecureFlag = "insecure";
    private const string JsonFlag = "json";
    private const string TimeoutOption = "timeout";

    private const string PasswordEnvironmentVariable = "SERVERGUARD_PASSWORD";
    private const string IngestKeyEnvironmentVariable = "SERVERGUARD_INGEST_KEY";
    private const string UrlEnvironmentVariable = "SERVERGUARD_URL";

    private const int DefaultTimeoutSeconds = 20;
    private const int FailureExitCode = 1;
    private const int SuccessExitCode = 0;

    public string Name => "check";

    public string Description => "API'nin sagligini ve guvenlik kurallarini disaridan dogrular.";

    public string Usage =>
        "ServerGuard.Tools check --url https://panel.example.local [--user admin] [--password ***] " +
        "[--ingest-key ***] [--insecure] [--json] [--timeout 20]";

    public async Task<int> RunAsync(CommandLineArguments arguments, CancellationToken cancellationToken)
    {
        var rawUrl = arguments.GetValueOrEnvironment(UrlOption, UrlEnvironmentVariable);

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var baseAddress))
        {
            Console.Error.WriteLine($"Gecerli bir adres verin. Ornek: {Usage}");
            return FailureExitCode;
        }

        var settings = BuildSettings(arguments, baseAddress);

        using var handler = BuildHandler(settings);
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = settings.Timeout
        };

        var results = await new ApiProbe(httpClient, settings).RunAsync(cancellationToken);

        if (arguments.HasFlag(JsonFlag))
        {
            ProbeReporter.WriteJson(baseAddress, results);
        }
        else
        {
            ProbeReporter.WriteText(baseAddress, results);
        }

        return ProbeReporter.HasFailure(results) ? FailureExitCode : SuccessExitCode;
    }

    private static ProbeSettings BuildSettings(CommandLineArguments arguments, Uri baseAddress) =>
        new(
            baseAddress,
            arguments.GetValue(UserOption),
            arguments.GetValueOrEnvironment(PasswordOption, PasswordEnvironmentVariable),
            arguments.GetValueOrEnvironment(IngestKeyOption, IngestKeyEnvironmentVariable),
            arguments.HasFlag(InsecureFlag),
            ResolveTimeout(arguments));

    private static TimeSpan ResolveTimeout(CommandLineArguments arguments)
    {
        var raw = arguments.GetValue(TimeoutOption);

        return int.TryParse(raw, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(DefaultTimeoutSeconds);
    }

    /// <summary>
    /// Sertifika doğrulaması yalnızca açıkça istendiğinde kapatılır. Kapatmak, tam da
    /// bulunması gereken bir sorunu (geçersiz sertifika) görünmez kılar; bu yüzden
    /// kapatıldığında uyarı yazılır.
    /// </summary>
    private static HttpClientHandler BuildHandler(ProbeSettings settings)
    {
        var handler = new HttpClientHandler();

        if (!settings.AllowUntrustedCertificate)
        {
            return handler;
        }

        Console.Error.WriteLine("UYARI: Sertifika dogrulamasi kapatildi (--insecure). Production kontrolunde kullanmayin.");
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        return handler;
    }
}
