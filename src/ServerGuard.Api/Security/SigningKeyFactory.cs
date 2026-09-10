using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ServerGuard.Api.Security;

/// <summary>
/// İmza anahtarını yapılandırmadaki metinden üretir. Token üretimi ve doğrulaması
/// aynı kaynaktan beslensin diye tek yerde toplanmıştır.
/// </summary>
public static class SigningKeyFactory
{
    public static SymmetricSecurityKey Create(string signingKey) =>
        new(Encoding.UTF8.GetBytes(signingKey));
}
