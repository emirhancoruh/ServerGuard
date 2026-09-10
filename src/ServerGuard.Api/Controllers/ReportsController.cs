using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServerGuard.Api.Contracts;
using ServerGuard.Api.Repositories;
using ServerGuard.Api.Security;
using ServerGuard.Api.Throttling;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthorizationPolicies.Panel)]
[EnableRateLimiting(RateLimitPolicies.Panel)]
[Route(ApiRoutes.Reports)]
public sealed class ReportsController(
    IValidator<ReportSummaryQuery> validator,
    IReportRepository repository,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet(ApiRoutes.SummarySegment)]
    [ProducesResponseType<ReportSummaryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary([FromQuery] ReportSummaryQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var (from, to) = ResolveRange(query);

        var summary = await repository.GetSummaryAsync(query.ServerName, from, to, cancellationToken);

        return Ok(summary);
    }

    /// <summary>
    /// Verilmeyen tarihleri tamamlar. Yalnızca <c>from</c> verilmişse üst sınır şu andır;
    /// hiçbiri verilmemişse varsayılan aralık kadar geriye gidilir.
    /// </summary>
    private (DateTimeOffset From, DateTimeOffset To) ResolveRange(ReportSummaryQuery query)
    {
        var to = query.To ?? timeProvider.GetUtcNow();
        var from = query.From ?? to.AddDays(-ReportQueryConstraints.DefaultRangeDays);

        return (from, to);
    }
}
