using ServerGuard.Shared;
using ServerGuard.Shared.Security;

namespace ServerGuard.Api.Security;

/// <summary>
/// Güvenlik yapılandırmasındaki eksikleri bulur. Yalnızca tespit eder; ne yapılacağına
/// (açılışı durdurmak veya uyarmak) çağıran taraf karar verir.
/// </summary>
public static class SecurityConfigurationInspector
{
    public static IReadOnlyList<string> FindProblems(SecurityOptions options)
    {
        List<string> problems = [];

        InspectJwt(options.Jwt, problems);
        InspectPanel(options.Panel, problems);
        InspectIngest(options.Ingest, problems);

        return problems;
    }

    private static void InspectJwt(JwtOptions jwt, List<string> problems)
    {
        if (string.IsNullOrWhiteSpace(jwt.SigningKey))
        {
            problems.Add(
                "Security:Jwt:SigningKey tanimli degil. Panel oturum token'lari imzalanamaz. " +
                "Ortam degiskeni: Security__Jwt__SigningKey");
        }
        else if (jwt.SigningKey.Length < JwtOptions.MinimumSigningKeyLength)
        {
            problems.Add(
                $"Security:Jwt:SigningKey en az {JwtOptions.MinimumSigningKeyLength} karakter olmali " +
                $"(su an {jwt.SigningKey.Length}).");
        }

        if (jwt.AccessTokenLifetime <= TimeSpan.Zero)
        {
            problems.Add("Security:Jwt:AccessTokenLifetime sifirdan buyuk olmali.");
        }

        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
        {
            problems.Add("Security:Jwt:Issuer ve Security:Jwt:Audience bos birakilamaz.");
        }
    }

    private static void InspectPanel(PanelOptions panel, List<string> problems)
    {
        if (panel.Users.Count == 0)
        {
            problems.Add(
                "Security:Panel:Users bos. Panele kimse giris yapamaz. " +
                "Ortam degiskenleri: Security__Panel__Users__0__UserName / Security__Panel__Users__0__PasswordHash");
            return;
        }

        for (var index = 0; index < panel.Users.Count; index++)
        {
            var user = panel.Users[index];

            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                problems.Add($"Security:Panel:Users[{index}]:UserName bos.");
            }

            if (!PasswordHash.IsValidFormat(user.PasswordHash))
            {
                problems.Add(
                    $"Security:Panel:Users[{index}]:PasswordHash gecerli bir PBKDF2 ozeti degil. " +
                    "'ServerGuard.Tools hash-password' ile uretin.");
            }
        }

        if (panel.MaxFailedAttempts < 1)
        {
            problems.Add("Security:Panel:MaxFailedAttempts en az 1 olmali.");
        }

        if (panel.LockoutDuration <= TimeSpan.Zero)
        {
            problems.Add("Security:Panel:LockoutDuration sifirdan buyuk olmali.");
        }

        if (panel.TrackedAttemptLimit < 1)
        {
            problems.Add("Security:Panel:TrackedAttemptLimit en az 1 olmali.");
        }
    }

    private static void InspectIngest(IngestOptions ingest, List<string> problems)
    {
        if (ingest.ApiKeys.Count == 0)
        {
            problems.Add(
                "Security:Ingest:ApiKeys bos. Agent'lar veri gonderemez. " +
                "Ortam degiskenleri: Security__Ingest__ApiKeys__0__Name / Security__Ingest__ApiKeys__0__Key");
            return;
        }

        for (var index = 0; index < ingest.ApiKeys.Count; index++)
        {
            var apiKey = ingest.ApiKeys[index];

            if (string.IsNullOrWhiteSpace(apiKey.Name))
            {
                problems.Add($"Security:Ingest:ApiKeys[{index}]:Name bos. Log'larda hangi agent oldugu ayirt edilemez.");
            }

            if (apiKey.Key.Length < AuthConstraints.MinimumSecretLength)
            {
                problems.Add(
                    $"Security:Ingest:ApiKeys[{index}]:Key en az {AuthConstraints.MinimumSecretLength} karakter olmali. " +
                    "'ServerGuard.Tools new-key' ile uretin.");
            }
        }
    }
}
