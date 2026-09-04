using FluentValidation;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Validation;

public sealed class TrafficLogDtoValidator : AbstractValidator<TrafficLogDto>
{
    public TrafficLogDtoValidator()
    {
        RuleFor(dto => dto.ServerName)
            .NotEmpty()
            .MaximumLength(ServerConstraints.NameMaxLength);

        RuleFor(dto => dto.ClientIp)
            .NotEmpty()
            .MaximumLength(TrafficConstraints.ClientIpMaxLength);

        RuleFor(dto => dto.RequestPath)
            .NotEmpty()
            .MaximumLength(TrafficConstraints.RequestPathMaxLength);

        RuleFor(dto => dto.StatusCode)
            .InclusiveBetween(TrafficConstraints.MinStatusCode, TrafficConstraints.MaxStatusCode);

        RuleFor(dto => dto.ResponseTimeMs)
            .InclusiveBetween(TrafficConstraints.MinResponseTimeMs, TrafficConstraints.MaxResponseTimeMs);
    }
}
