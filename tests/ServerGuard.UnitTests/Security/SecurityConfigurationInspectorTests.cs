using ServerGuard.Api.Security;
using ServerGuard.Shared.Security;

namespace ServerGuard.UnitTests.Security;

/// <summary>
/// Yapılandırma denetimi, production'da açılışı durduran karardır. Eksik bir ayarı gözden
/// kaçırırsa sistem yarı çalışır durumda yayına çıkar; sağlam bir ayarı hatalı bulursa
/// açılışı gereksiz yere engeller. İki yön de sınanır.
/// </summary>
public sealed class SecurityConfigurationInspectorTests
{
    [Fact]
    public void FindProblems_ReturnsNothingForCompleteConfiguration()
    {
        Assert.Empty(SecurityConfigurationInspector.FindProblems(CreateComplete()));
    }

    [Fact]
    public void FindProblems_ReportsMissingSigningKey()
    {
        var options = CreateComplete();
        options.Jwt.SigningKey = string.Empty;

        Assert.Contains(
            SecurityConfigurationInspector.FindProblems(options),
            problem => problem.Contains("Security__Jwt__SigningKey"));
    }

    [Fact]
    public void FindProblems_ReportsShortSigningKey()
    {
        var options = CreateComplete();
        options.Jwt.SigningKey = "kisa";

        Assert.Contains(
            SecurityConfigurationInspector.FindProblems(options),
            problem => problem.Contains("SigningKey"));
    }

    [Fact]
    public void FindProblems_ReportsMissingPanelUsers()
    {
        var options = CreateComplete();
        options.Panel.Users = [];

        Assert.Contains(
            SecurityConfigurationInspector.FindProblems(options),
            problem => problem.Contains("Security__Panel__Users__0__UserName"));
    }

    [Fact]
    public void FindProblems_ReportsPlainTextPassword()
    {
        // Ozet yerine duz parola yazilmasi, sessizce kabul edilirse giris hic calismaz.
        var options = CreateComplete();
        options.Panel.Users = [new PanelUserOptions { UserName = "admin", PasswordHash = "duz-parola" }];

        Assert.Contains(
            SecurityConfigurationInspector.FindProblems(options),
            problem => problem.Contains("PBKDF2"));
    }

    [Fact]
    public void FindProblems_ReportsMissingIngestKeys()
    {
        var options = CreateComplete();
        options.Ingest.ApiKeys = [];

        Assert.Contains(
            SecurityConfigurationInspector.FindProblems(options),
            problem => problem.Contains("Security__Ingest__ApiKeys__0__Key"));
    }

    [Fact]
    public void FindProblems_ReportsShortIngestKey()
    {
        var options = CreateComplete();
        options.Ingest.ApiKeys = [new IngestKeyOptions { Name = "SERVER10", Key = "kisa" }];

        Assert.Contains(
            SecurityConfigurationInspector.FindProblems(options),
            problem => problem.Contains("ApiKeys[0]:Key"));
    }

    [Fact]
    public void FindProblems_ReportsNonPositiveTokenLifetime()
    {
        var options = CreateComplete();
        options.Jwt.AccessTokenLifetime = TimeSpan.Zero;

        Assert.Contains(
            SecurityConfigurationInspector.FindProblems(options),
            problem => problem.Contains("AccessTokenLifetime"));
    }

    private static SecurityOptions CreateComplete() => new()
    {
        Jwt = new JwtOptions { SigningKey = SecretGenerator.Create() },
        Panel = new PanelOptions
        {
            Users =
            [
                new PanelUserOptions
                {
                    UserName = "admin",
                    PasswordHash = PasswordHash.Create("GuvenliParola123!", iterations: 1_000)
                }
            ]
        },
        Ingest = new IngestOptions
        {
            ApiKeys = [new IngestKeyOptions { Name = "SERVER10", Key = SecretGenerator.Create() }]
        }
    };
}
