using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ServerGuard.Api.Contracts;
using ServerGuard.Api.Repositories;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Servers)]
public sealed class ServersController(
    IValidator<ServerQuery> validator,
    IServerRepository repository,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ServerSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetKnownServers([FromQuery] ServerQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var since = timeProvider.GetUtcNow().AddHours(-query.SinceHours);
        var servers = await repository.GetKnownServersAsync(since, cancellationToken);

        return Ok(servers);
    }
}
