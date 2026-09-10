using Microsoft.Extensions.Options;
using ServerGuard.Api.Monitoring;
using ServerGuard.Shared.Enums;

namespace ServerGuard.UnitTests.Monitoring;

/// <summary>
/// Sunucunun çöktüğünü fark etmek panelin var oluş sebebidir; eşik sınırları tam olarak
/// sınanır. Sınırın kendisi "dahil" sayılır: eşiğe ulaşan sessizlik artık o durumdadır.
/// </summary>
public sealed class ServerHealthEvaluatorTests
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(3);

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(59)]
    public void Evaluate_ReturnsOnlineBelowStaleThreshold(int seconds)
    {
        Assert.Equal(ServerHealthStatus.Online, Evaluate(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(90)]
    [InlineData(179)]
    public void Evaluate_ReturnsStaleBetweenThresholds(int seconds)
    {
        Assert.Equal(ServerHealthStatus.Stale, Evaluate(TimeSpan.FromSeconds(seconds)));
    }

    [Theory]
    [InlineData(180)]
    [InlineData(600)]
    [InlineData(86_400)]
    public void Evaluate_ReturnsOfflineAtOrAboveOfflineThreshold(int seconds)
    {
        Assert.Equal(ServerHealthStatus.Offline, Evaluate(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Evaluate_TreatsNegativeDurationAsOnline()
    {
        // Sunucu saati ileri kaymissa gecen sure negatif cikabilir; bu, sessizlik degildir.
        Assert.Equal(ServerHealthStatus.Online, Evaluate(TimeSpan.FromSeconds(-30)));
    }

    private static ServerHealthStatus Evaluate(TimeSpan sinceLastSeen)
    {
        var options = Options.Create(new ServerHealthOptions
        {
            StaleAfter = StaleAfter,
            OfflineAfter = OfflineAfter
        });

        return new ServerHealthEvaluator(options).Evaluate(sinceLastSeen);
    }
}
