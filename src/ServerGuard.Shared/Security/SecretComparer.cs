using System.Security.Cryptography;
using System.Text;

namespace ServerGuard.Shared.Security;

/// <summary>
/// Sırları sabit zamanda karşılaştırır. Sıradan dize karşılaştırması ilk farklı karakterde
/// döneceğinden, yanıt süresi ölçülerek anahtar karakter karakter tahmin edilebilir.
/// </summary>
public static class SecretComparer
{
    public static bool Equals(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
