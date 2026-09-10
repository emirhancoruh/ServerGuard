using Microsoft.Extensions.Options;

namespace ServerGuard.Api.Security;

/// <summary>
/// Production'da eksik güvenlik yapılandırmasıyla açılışı engeller.
/// </summary>
/// <remarks>
/// Geliştirmede engellemez; aksi halde depoyu yeni klonlayan biri projeyi hiç çalıştıramazdı.
/// Eksikler o durumda <see cref="SecurityConfigurationAudit"/> tarafından uyarı olarak yazılır.
/// </remarks>
public sealed class SecurityOptionsValidator(IHostEnvironment environment) : IValidateOptions<SecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        var problems = SecurityConfigurationInspector.FindProblems(options);

        if (problems.Count == 0 || !environment.IsProduction())
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(problems);
    }
}
