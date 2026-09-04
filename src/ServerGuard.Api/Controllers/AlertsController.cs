using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ServerGuard.Api.Contracts;
using ServerGuard.Api.Repositories;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Alerts)]
public sealed class AlertsController(
    IValidator<AlertQuery> validator,
    ISecurityAlertRepository repository) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<SecurityAlertDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Query([FromQuery] AlertQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validation.ToDictionary()));
        }

        var result = await repository.QueryAsync(query, cancellationToken);

        return Ok(result);
    }
}
