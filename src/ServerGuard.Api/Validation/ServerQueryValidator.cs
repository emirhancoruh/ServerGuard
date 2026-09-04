using FluentValidation;
using ServerGuard.Api.Contracts;
using ServerGuard.Shared;

namespace ServerGuard.Api.Validation;

public sealed class ServerQueryValidator : AbstractValidator<ServerQuery>
{
    public ServerQueryValidator()
    {
        RuleFor(query => query.SinceHours)
            .InclusiveBetween(ServerQueryConstraints.MinSinceHours, ServerQueryConstraints.MaxSinceHours);
    }
}
