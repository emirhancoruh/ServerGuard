using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerGuard.Api.Contracts;
using ServerGuard.Api.Monitoring;
using ServerGuard.Api.Security;
using ServerGuard.Api.Throttling;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Panel)]
[EnableRateLimiting(RateLimitPolicies.Panel)]
[Route(ApiRoutes.Overview)]
public sealed class OverviewController(
    IValidator<TrafficRangeQuery> validator,
    IMonitoringOverviewService overviewService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MonitoringOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromQuery] TrafficRangeQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var overview = await overviewService.GetOverviewAsync(query.ServerName, query.Minutes, cancellationToken);

        return Ok(overview);
    }
}
