using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ServerGuard.Shared;

namespace ServerGuard.Api.Security;

/// <summary>
/// Agent isteklerini <c>X-ServerGuard-Key</c> header'ındaki anahtara göre doğrular.
/// </summary>
/// <remarks>
/// Anahtar hiçbir koşulda loglanmaz; reddedilen isteklerde yalnızca isteğin geldiği adres
/// ve yol yazılır. Header yoksa şema <see cref="AuthenticateResult.NoResult"/> döner;
/// böylece aynı endpoint'e başka bir şemayla erişim denemesi engellenmiş olmaz.
/// </remarks>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IIngestKeyValidator keyValidator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthConstraints.ApiKeyHeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presentedKey = headerValues.ToString();
        var agentName = keyValidator.ResolveAgentName(presentedKey);

        if (agentName is null)
        {
            Logger.LogWarning(
                "Rejected ingest request with an unknown API key. RemoteIp={RemoteIp} Path={Path}",
                Request.HttpContext.Connection.RemoteIpAddress,
                Request.Path);

            return Task.FromResult(AuthenticateResult.Fail("Gecersiz API anahtari."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, agentName),
                new Claim(AuthenticationDefaults.AgentNameClaimType, agentName)
            ],
            Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
