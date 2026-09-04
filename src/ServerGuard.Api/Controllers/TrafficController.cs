using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ServerGuard.Api.Contracts;
using ServerGuard.Api.Detection;
using ServerGuard.Api.Mapping;
using ServerGuard.Api.Realtime;
using ServerGuard.Api.Repositories;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Controllers;

[ApiController]
[Route(ApiRoutes.TrafficLogs)]
public sealed class TrafficController(
    IValidator<TrafficLogDto> validator,
    IValidator<TrafficTimelineQuery> timelineValidator,
    IValidator<TopClientIpQuery> topClientIpValidator,
    ITrafficLogRepository repository,
    IMonitoringBroadcaster broadcaster,
    ITrafficAnomalyDetectionService anomalyDetection,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpPost]
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

    /// <summary>Sorgu aralığını şu andan geriye doğru hesaplar.</summary>
    private (DateTimeOffset From, DateTimeOffset To) ResolveRange(int minutes)
    {
        var to = timeProvider.GetUtcNow();
        return (to.AddMinutes(-minutes), to);
    }
}
