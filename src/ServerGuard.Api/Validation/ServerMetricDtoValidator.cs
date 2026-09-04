using FluentValidation;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Validation;

public sealed class ServerMetricDtoValidator : AbstractValidator<ServerMetricDto>
{
    public ServerMetricDtoValidator()
    {
        RuleFor(dto => dto.ServerName)
            .NotEmpty()
            .MaximumLength(ServerConstraints.NameMaxLength);

        RuleFor(dto => dto.CpuUsagePercent)
            .InclusiveBetween(MetricConstraints.MinPercent, MetricConstraints.MaxPercent);

        RuleFor(dto => dto.RamUsagePercent)
            .InclusiveBetween(MetricConstraints.MinPercent, MetricConstraints.MaxPercent);
    }
}
