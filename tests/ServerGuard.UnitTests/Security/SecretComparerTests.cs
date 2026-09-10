using ServerGuard.Shared.Security;

namespace ServerGuard.UnitTests.Security;

/// <summary>
/// Sabit zamanlı karşılaştırma, sıradan eşitlik ile aynı sonucu vermelidir; güvenlik uğruna
/// doğruluktan ödün verilmemelidir.
/// </summary>
public sealed class SecretComparerTests
{
    [Fact]
    public void Equals_ReturnsTrueForIdenticalSecrets()
    {
        Assert.True(SecretComparer.Equals("anahtar-degeri", "anahtar-degeri"));
    }

    [Theory]
    [InlineData("anahtar", "anahtaR")]
    [InlineData("anahtar", "anahtar ")]
    [InlineData("anahtar", "anahta")]
    [InlineData("anahtar", "")]
    [InlineData("", "anahtar")]
    public void Equals_ReturnsFalseForDifferentSecrets(string left, string right)
    {
        Assert.False(SecretComparer.Equals(left, right));
    }

    [Fact]
    public void Equals_ReturnsFalseWhenEitherSideIsNull()
    {
        Assert.False(SecretComparer.Equals(null, "anahtar"));
        Assert.False(SecretComparer.Equals("anahtar", null));
        Assert.False(SecretComparer.Equals(null, null));
    }

    [Fact]
    public void Equals_HandlesNonAsciiCharacters()
    {
        Assert.True(SecretComparer.Equals("şifreÇĞİ", "şifreÇĞİ"));
        Assert.False(SecretComparer.Equals("şifreÇĞİ", "sifreCGI"));
    }

    [Fact]
    public void Create_GeneratesDistinctUrlSafeSecrets()
    {
        var first = SecretGenerator.Create();
        var second = SecretGenerator.Create();

        Assert.NotEqual(first, second);
        Assert.DoesNotContain('+', first);
        Assert.DoesNotContain('/', first);
        Assert.DoesNotContain('=', first);
    }
}
