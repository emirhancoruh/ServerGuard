using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Security;

/// <summary>
/// Açılışta güvenlik yapılandırmasının durumunu log'a yazar. Production'da açılış zaten
/// engellendiğinden bu, geliştirme ortamında eksiklerin sessizce gözden kaçmasını önler.
/// </summary>
public sealed class SecurityConfigurationAudit(
    IOptions<SecurityOptions> options,
    ILogger<SecurityConfigurationAudit> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var value = options.Value;
        var problems = SecurityConfigurationInspector.FindProblems(value);

        if (problems.Count == 0)
        {
            logger.LogInformation(
                "Security configuration is complete. PanelUsers={UserCount} IngestKeys={KeyCount} RequireHttps={RequireHttps}",
                value.Panel.Users.Count,
                value.Ingest.ApiKeys.Count,
                value.RequireHttps);

            return Task.CompletedTask;
        }

        foreach (var problem in problems)
        {
            logger.LogWarning("Security configuration problem: {Problem}", problem);
        }

        logger.LogWarning(
            "{Count} security configuration problem(s) found. The API would refuse to start in Production.",
            problems.Count);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
