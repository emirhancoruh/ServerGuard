using System.Security.Cryptography;

namespace ServerGuard.Shared.Security;

/// <summary>
/// Panel parolalarını PBKDF2-SHA256 ile özetler ve doğrular.
/// </summary>
/// <remarks>
/// Parolanın kendisi hiçbir yerde saklanmaz; yapılandırmaya yalnızca bu sınıfın ürettiği
/// özet yazılır. Özet biçimi kendini tanımlar (<c>pbkdf2-sha256$yineleme$tuz$ozet</c>),
/// böylece yineleme sayısı ileride artırılsa da eski özetler doğrulanmaya devam eder.
/// Karşılaştırma sabit zamanlıdır; yanıt süresinden özet hakkında bilgi sızmaz.
/// </remarks>
public static class PasswordHash
{
    public const string Prefix = "pbkdf2-sha256";

    private const char FieldSeparator = '$';
    private const int ExpectedFieldCount = 4;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int DefaultIterations = 210_000;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>Yapılandırmaya yazılacak, kendini tanımlayan özet dizesini üretir.</summary>
    public static string Create(string password, int iterations = DefaultIterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, iterations);

        return string.Join(
            FieldSeparator,
            Prefix,
            iterations.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Parolayı özetle karşılaştırır. Özet bozuksa <c>false</c> döner; çağıran taraf
    /// bunu "eşleşmedi" olarak ele alır, istisna fırlatılmaz.
    /// </summary>
    public static bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(encodedHash))
        {
            return false;
        }

        if (!TryDecode(encodedHash, out var iterations, out var salt, out var expected))
        {
            return false;
        }

        var actual = Derive(password, salt, iterations);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Yapılandırmadaki bir değerin geçerli bir özet olup olmadığını söyler.</summary>
    public static bool IsValidFormat(string? encodedHash) =>
        !string.IsNullOrWhiteSpace(encodedHash) && TryDecode(encodedHash, out _, out _, out _);

    private static bool TryDecode(
        string encodedHash,
        out int iterations,
        out byte[] salt,
        out byte[] hash)
    {
        iterations = 0;
        salt = [];
        hash = [];

        var parts = encodedHash.Split(FieldSeparator);

        if (parts.Length != ExpectedFieldCount || parts[0] != Prefix)
        {
            return false;
        }

        if (!int.TryParse(parts[1], out iterations) || iterations < 1)
        {
            return false;
        }

        return TryDecodeBase64(parts[2], out salt) && TryDecodeBase64(parts[3], out hash);
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        var buffer = new byte[GetMaxDecodedLength(value.Length)];

        if (Convert.TryFromBase64String(value, buffer, out var written) && written > 0)
        {
            bytes = buffer[..written];
            return true;
        }

        bytes = [];
        return false;
    }

    private static int GetMaxDecodedLength(int base64Length) => base64Length / 4 * 3 + 3;

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, HashBytes);
}
