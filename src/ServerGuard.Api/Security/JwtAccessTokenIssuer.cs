using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Security;

/// <summary>
/// HMAC-SHA256 ile imzalanmış, kısa ömürlü JWT üretir.
/// </summary>
/// <remarks>
/// Token sunucuda saklanmaz; doğrulama tamamen imzaya dayanır. Bu nedenle tek tek iptal
/// edilemez, ömrü kısa tutulur. Bir kullanıcının erişimini derhal kesmek gerekirse
/// yapılandırmadan çıkarılıp imza anahtarı değiştirilir; bu, tüm oturumları geçersiz kılar.
/// </remarks>
public sealed class JwtAccessTokenIssuer(
    IOptions<SecurityOptions> options,
    TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JwtOptions _options = options.Value.Jwt;
    private readonly JsonWebTokenHandler _handler = new();

    public LoginResponseDto Issue(string userName)
    {
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.Add(_options.AccessTokenLifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            SigningCredentials = new SigningCredentials(
                SigningKeyFactory.Create(_options.SigningKey),
                SecurityAlgorithms.HmacSha256)
        };

        return new LoginResponseDto(_handler.CreateToken(descriptor), expiresAt, userName);
    }
}
