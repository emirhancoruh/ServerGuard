using FluentValidation;
using ServerGuard.Api.Contracts;
using ServerGuard.Shared;

namespace ServerGuard.Api.Validation;

public sealed class TrafficTimelineQueryValidator : AbstractValidator<TrafficTimelineQuery>
{
    public TrafficTimelineQueryValidator()
    {
        RuleFor(query => query.ServerName)
            .MaximumLength(ServerConstraints.NameMaxLength);

        RuleFor(query => query.Minutes)
            .InclusiveBetween(TrafficQueryConstraints.MinMinutes, TrafficQueryConstraints.MaxMinutes);

        RuleFor(query => query.BucketSeconds)
            .InclusiveBetween(TrafficQueryConstraints.MinBucketSeconds, TrafficQueryConstraints.MaxBucketSeconds);
    }
}
