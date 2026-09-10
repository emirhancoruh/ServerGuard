using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using ServerGuard.Api.Security;
using ServerGuard.Api.Throttling;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Controllers;

/// <summary>
/// Panel oturumu açma ve mevcut oturumu doğrulama.
/// </summary>
[ApiController]
[Route(ApiRoutes.Auth)]
public sealed class AuthController(
    IValidator<LoginRequestDto> validator,
    IPanelAuthenticator authenticator,
    IAccessTokenIssuer tokenIssuer) : ControllerBase
{
    private const string InvalidCredentialsMessage = "Kullanıcı adı veya parola hatalı.";
    private const string LockedOutMessage =
        "Hesap art arda başarısız denemeler nedeniyle geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.";

    /// <summary>
    /// Kimlik bilgilerini doğrular ve kısa ömürlü bir erişim token'ı döner.
    /// </summary>
    /// <remarks>
    /// Kullanıcı adının var olup olmadığı yanıtta ayırt edilmez; her iki durumda da aynı
    /// mesaj ve aynı durum kodu döner.
    /// </remarks>
    [HttpPost(ApiRoutes.LoginSegment)]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = authenticator.Authenticate(request.UserName, request.Password, out var canonicalUserName);

        return result switch
        {
            PanelAuthenticationResult.Succeeded => Ok(tokenIssuer.Issue(canonicalUserName)),
            PanelAuthenticationResult.LockedOut => Problem(
                detail: LockedOutMessage,
                statusCode: StatusCodes.Status401Unauthorized),
            _ => Problem(
                detail: InvalidCredentialsMessage,
                statusCode: StatusCodes.Status401Unauthorized)
        };
    }

    /// <summary>
    /// Geçerli token'ın sahibini döner. Panel, açılışta oturumun hâlâ geçerli olduğunu
    /// bu uçla doğrular.
    /// </summary>
    [HttpGet(ApiRoutes.CurrentUserSegment)]
    [Authorize(Policy = AuthorizationPolicies.Panel)]
    [EnableRateLimiting(RateLimitPolicies.Panel)]
    [ProducesResponseType<AuthenticatedUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userName = User.Identity?.Name ?? string.Empty;

        return Ok(new AuthenticatedUserDto(userName, ResolveExpiry()));
    }

    /// <summary>
    /// Token'ın bitiş zamanını claim'den okur. Claim Unix saniyesi olarak taşınır.
    /// </summary>
    private DateTimeOffset ResolveExpiry()
    {
        var raw = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        return long.TryParse(raw, out var unixSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : DateTimeOffset.MinValue;
    }
}
