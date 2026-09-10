using Microsoft.Extensions.Options;
using ServerGuard.Shared.Security;

namespace ServerGuard.Api.Security;

/// <summary>
/// Yapılandırmadaki kullanıcı listesine karşı parola doğrular.
/// </summary>
/// <remarks>
/// Kullanıcı bulunamadığında da bir özet doğrulaması çalıştırılır. Aksi halde var olmayan
/// kullanıcı için yanıt belirgin biçimde daha hızlı döner ve hangi kullanıcı adlarının
/// geçerli olduğu yanıt süresinden anlaşılabilirdi.
/// </remarks>
public sealed class PanelAuthenticator(
    IOptions<SecurityOptions> options,
    LoginAttemptTracker attemptTracker,
    ILogger<PanelAuthenticator> logger) : IPanelAuthenticator
{
    /// <summary>
    /// Kullanıcı bulunamadığında karşılaştırılacak sabit özet. Değeri önemsizdir; amacı
    /// yalnızca aynı hesaplama maliyetini oluşturmaktır.
    /// </summary>
    private static readonly string DecoyHash = PasswordHash.Create(SecretGenerator.Create());

    private readonly IReadOnlyList<PanelUserOptions> _users = options.Value.Panel.Users;

    public PanelAuthenticationResult Authenticate(string userName, string password, out string canonicalUserName)
    {
        canonicalUserName = userName;

        if (attemptTracker.IsLockedOut(userName))
        {
            logger.LogWarning("Login rejected: account is locked out. UserName={UserName}", userName);
            return PanelAuthenticationResult.LockedOut;
        }

        var user = FindUser(userName);
        var isValid = PasswordHash.Verify(password, user?.PasswordHash ?? DecoyHash);

        if (user is null || !isValid)
        {
            var lockedOut = attemptTracker.RegisterFailure(userName);

            logger.LogWarning(
                "Login failed. UserName={UserName} LockedOut={LockedOut}",
                userName,
                lockedOut);

            return lockedOut ? PanelAuthenticationResult.LockedOut : PanelAuthenticationResult.InvalidCredentials;
        }

        attemptTracker.Reset(userName);
        canonicalUserName = user.UserName;

        logger.LogInformation("Login succeeded. UserName={UserName}", canonicalUserName);

        return PanelAuthenticationResult.Succeeded;
    }

    private PanelUserOptions? FindUser(string userName)
    {
        foreach (var user in _users)
        {
            if (string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                return user;
            }
        }

        return null;
    }
}
