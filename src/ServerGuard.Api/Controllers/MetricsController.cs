using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerGuard.Api.Contracts;
using ServerGuard.Api.Mapping;
using ServerGuard.Api.Realtime;
using ServerGuard.Api.Repositories;
using ServerGuard.Api.Security;
using ServerGuard.Api.Throttling;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Ingest)]
[EnableRateLimiting(RateLimitPolicies.Ingest)]
[Route(ApiRoutes.Metrics)]
public sealed class MetricsController(
    IValidator<ServerMetricDto> validator,
    IServerMetricRepository repository,
    IMonitoringBroadcaster broadcaster,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreatedResourceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(ServerMetricDto dto, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var saved = await repository.AddAsync(dto.ToEntity(timeProvider.GetUtcNow()), cancellationToken);

        await broadcaster.BroadcastMetricAsync(dto, cancellationToken);

        return Created((string?)null, new CreatedResourceResponse(saved.Id));
    }
}
