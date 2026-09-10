using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerGuard.Api.Contracts;
using ServerGuard.Api.Detection;
using ServerGuard.Api.Mapping;
using ServerGuard.Api.Realtime;
using ServerGuard.Api.Repositories;
using ServerGuard.Api.Security;
using ServerGuard.Api.Throttling;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Controllers;

// Bu controller hem agent'in yazdigi hem panelin okudugu uclari barindirdigindan
// yetki ve hiz siniri sinif duzeyinde degil, her eylemde ayri tanimlanir.
[ApiController]
[Route(ApiRoutes.TrafficLogs)]
public sealed class TrafficController(
    IValidator<TrafficLogDto> validator,
    IValidator<TrafficTimelineQuery> timelineValidator,
    IValidator<TopClientIpQuery> topClientIpValidator,
    IValidator<TrafficRangeQuery> rangeValidator,
    ITrafficLogRepository repository,
    IMonitoringBroadcaster broadcaster,
    ITrafficAnomalyDetectionService anomalyDetection,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Ingest)]
    [EnableRateLimiting(RateLimitPolicies.Ingest)]
    [ProducesResponseType<CreatedResourceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(TrafficLogDto dto, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var saved = await repository.AddAsync(dto.ToEntity(timeProvider.GetUtcNow()), cancellationToken);

        await broadcaster.BroadcastTrafficLogAsync(dto, cancellationToken);
        await anomalyDetection.InspectAsync(dto, cancellationToken);

        return Created((string?)null, new CreatedResourceResponse(saved.Id));
    }

    [HttpGet(ApiRoutes.TimelineSegment)]
    [Authorize(Policy = AuthorizationPolicies.Panel)]
    [EnableRateLimiting(RateLimitPolicies.Panel)]
    [ProducesResponseType<IReadOnlyList<TrafficTimelinePointDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTimeline([FromQuery] TrafficTimelineQuery query, CancellationToken cancellationToken)
    {
        var validation = await timelineValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var (from, to) = ResolveRange(query.Minutes);

        var timeline = await repository.GetTimelineAsync(
            query.ServerName,
            from,
            to,
            query.BucketSeconds,
            cancellationToken);

        return Ok(timeline);
    }

    [HttpGet(ApiRoutes.TopClientIpsSegment)]
    [Authorize(Policy = AuthorizationPolicies.Panel)]
    [EnableRateLimiting(RateLimitPolicies.Panel)]
    [ProducesResponseType<IReadOnlyList<TopClientIpDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopClientIps([FromQuery] TopClientIpQuery query, CancellationToken cancellationToken)
    {
        var validation = await topClientIpValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var (from, to) = ResolveRange(query.Minutes);

        var topClientIps = await repository.GetTopClientIpsAsync(
            query.ServerName,
            from,
            to,
            query.Take,
            cancellationToken);

        return Ok(topClientIps);
    }

    [HttpGet(ApiRoutes.ServiceHealthSegment)]
    [Authorize(Policy = AuthorizationPolicies.Panel)]
    [EnableRateLimiting(RateLimitPolicies.Panel)]
    [ProducesResponseType<IReadOnlyList<ServiceHealthDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetServiceHealth([FromQuery] TrafficRangeQuery query, CancellationToken cancellationToken)
    {
        var validation = await rangeValidator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var (from, to) = ResolveRange(query.Minutes);

        var services = await repository.GetServiceHealthAsync(query.ServerName, from, to, cancellationToken);

        return Ok(services);
    }

    /// <summary>Sorgu aralığını şu andan geriye doğru hesaplar.</summary>
    private (DateTimeOffset From, DateTimeOffset To) ResolveRange(int minutes)
    {
        var to = timeProvider.GetUtcNow();
        return (to.AddMinutes(-minutes), to);
    }
}
