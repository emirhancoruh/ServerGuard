using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Security;

/// <summary>Panel oturum token'ı üretir.</summary>
public interface IAccessTokenIssuer
{
    LoginResponseDto Issue(string userName);
}
