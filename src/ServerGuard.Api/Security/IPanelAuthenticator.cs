namespace ServerGuard.Api.Security;

/// <summary>Panel kullanıcısının kimliğini doğrular.</summary>
public interface IPanelAuthenticator
{
    /// <summary>
    /// Kimlik bilgilerini doğrular. Başarılıysa yapılandırmadaki kanonik kullanıcı adı
    /// <paramref name="canonicalUserName"/> ile döner.
    /// </summary>
    PanelAuthenticationResult Authenticate(string userName, string password, out string canonicalUserName);
}
