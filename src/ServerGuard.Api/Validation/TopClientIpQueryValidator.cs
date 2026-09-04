using FluentValidation;
using ServerGuard.Api.Contracts;
using ServerGuard.Shared;

namespace ServerGuard.Api.Validation;

public sealed class TopClientIpQueryValidator : AbstractValidator<TopClientIpQuery>
{
    public TopClientIpQueryValidator()
    {
        RuleFor(query => query.ServerName)
            .MaximumLength(ServerConstraints.NameMaxLength);

        RuleFor(query => query.Minutes)
            .InclusiveBetween(TrafficQueryConstraints.MinMinutes, TrafficQueryConstraints.MaxMinutes);

        RuleFor(query => query.Take)
            .InclusiveBetween(TrafficQueryConstraints.MinTake, TrafficQueryConstraints.MaxTake);
    }
}
