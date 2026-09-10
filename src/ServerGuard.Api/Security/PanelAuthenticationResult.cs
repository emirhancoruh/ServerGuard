namespace ServerGuard.Api.Security;

/// <summary>Panel giriş denemesinin sonucu.</summary>
public enum PanelAuthenticationResult
{
    /// <summary>Kullanıcı adı veya parola hatalı. İkisi ayrı ayrı belirtilmez.</summary>
    InvalidCredentials,

    /// <summary>Art arda başarısız denemeler nedeniyle hesap geçici olarak kilitli.</summary>
    LockedOut,

    Succeeded
}
