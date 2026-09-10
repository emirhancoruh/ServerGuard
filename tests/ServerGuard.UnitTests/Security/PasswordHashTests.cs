using ServerGuard.Shared.Security;

namespace ServerGuard.UnitTests.Security;

/// <summary>
/// Parola özetleme, yanlış parolayı reddetmenin yanı sıra <b>bozuk girdide de</b> reddetmelidir.
/// Bir özet ayrıştırılamadığında istisna fırlatmak, yapılandırma hatasını çalışma zamanı
/// çökmesine dönüştürürdü; beklenen davranış sessizce "eşleşmedi" demektir.
/// </summary>
public sealed class PasswordHashTests
{
    private const string Password = "GuvenliParola123!";

    [Fact]
    public void Create_ThenVerify_AcceptsCorrectPassword()
    {
        var hash = PasswordHash.Create(Password);

        Assert.True(PasswordHash.Verify(Password, hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = PasswordHash.Create(Password);

        Assert.False(PasswordHash.Verify("BaskaParola456!", hash));
    }

    [Fact]
    public void Create_ProducesDifferentHashesForSamePassword()
    {
        // Her özet kendi tuzunu taşır; aynı parola iki kez özetlendiğinde sonuç farklı olmalıdır,
        // aksi halde özetler karşılaştırılarak aynı parolayı kullanan hesaplar tespit edilebilirdi.
        Assert.NotEqual(PasswordHash.Create(Password), PasswordHash.Create(Password));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("duz-metin-parola")]
    [InlineData("pbkdf2-sha256$210000$eksik-alan")]
    [InlineData("pbkdf2-sha256$sifir-degil$c2FsdA==$aGFzaA==")]
    [InlineData("bilinmeyen-algoritma$210000$c2FsdA==$aGFzaA==")]
    public void Verify_RejectsMalformedHashWithoutThrowing(string malformedHash)
    {
        Assert.False(PasswordHash.Verify(Password, malformedHash));
    }

    [Fact]
    public void Verify_RejectsTamperedHash()
    {
        var hash = PasswordHash.Create(Password);
        var tampered = hash[..^2] + (hash[^2] == 'A' ? "B=" : "A=");

        Assert.False(PasswordHash.Verify(Password, tampered));
    }

    [Fact]
    public void Verify_RejectsEmptyPassword()
    {
        var hash = PasswordHash.Create(Password);

        Assert.False(PasswordHash.Verify(string.Empty, hash));
    }

    [Fact]
    public void IsValidFormat_AcceptsGeneratedHashAndRejectsOthers()
    {
        Assert.True(PasswordHash.IsValidFormat(PasswordHash.Create(Password)));
        Assert.False(PasswordHash.IsValidFormat(null));
        Assert.False(PasswordHash.IsValidFormat("duz-metin"));
    }

    [Fact]
    public void Verify_AcceptsHashCreatedWithDifferentIterationCount()
    {
        // Özet biçimi yineleme sayısını kendi içinde taşır: sayı ileride artırılsa da
        // eski özetler doğrulanmaya devam etmelidir.
        var hash = PasswordHash.Create(Password, iterations: 1_000);

        Assert.True(PasswordHash.Verify(Password, hash));
    }

    [Fact]
    public void Create_RejectsEmptyPassword()
    {
        Assert.Throws<ArgumentException>(() => PasswordHash.Create("  "));
    }
}
