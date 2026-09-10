using Microsoft.Extensions.Options;
using ServerGuard.Api.Security;

namespace ServerGuard.UnitTests.Security;

/// <summary>
/// Agent anahtarı doğrulaması; yanlış anahtarın kabul edilmesi sistemdeki en ağır hata olurdu.
/// </summary>
public sealed class IngestKeyValidatorTests
{
    private const string ServerTenKey = "test-anahtari-SERVER10-0000000000000000000";
    private const string ServerElevenKey = "test-anahtari-SERVER11-1111111111111111111";

    [Fact]
    public void ResolveAgentName_ReturnsNameForKnownKey()
    {
        var validator = CreateValidator();

        Assert.Equal("SERVER10", validator.ResolveAgentName(ServerTenKey));
        Assert.Equal("SERVER11", validator.ResolveAgentName(ServerElevenKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("yanlis-anahtar")]
    [InlineData("test-anahtari-SERVER10-000000000000000000")]   // son karakter eksik
    [InlineData("TEST-ANAHTARI-server10-0000000000000000000")]  // buyuk/kucuk harf farkli
    public void ResolveAgentName_ReturnsNullForUnknownKey(string presentedKey)
    {
        var validator = CreateValidator();

        Assert.Null(validator.ResolveAgentName(presentedKey));
    }

    [Fact]
    public void ResolveAgentName_ReturnsNullWhenNoKeysConfigured()
    {
        var validator = new IngestKeyValidator(Options.Create(new SecurityOptions()));

        Assert.Null(validator.ResolveAgentName(ServerTenKey));
    }

    private static IngestKeyValidator CreateValidator()
    {
        var options = new SecurityOptions
        {
            Ingest = new IngestOptions
            {
                ApiKeys =
                [
                    new IngestKeyOptions { Name = "SERVER10", Key = ServerTenKey },
                    new IngestKeyOptions { Name = "SERVER11", Key = ServerElevenKey }
                ]
            }
        };

        return new IngestKeyValidator(Options.Create(options));
    }
}
