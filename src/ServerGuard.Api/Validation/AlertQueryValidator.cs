using FluentValidation;
using ServerGuard.Api.Contracts;
using ServerGuard.Shared;

namespace ServerGuard.Api.Validation;

public sealed class AlertQueryValidator : AbstractValidator<AlertQuery>
{
    public AlertQueryValidator()
    {
        RuleFor(query => query.ServerName)
            .MaximumLength(ServerConstraints.NameMaxLength);

        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(PaginationConstraints.MinPage);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(PaginationConstraints.MinPageSize, PaginationConstraints.MaxPageSize);

        RuleFor(query => query.To)
            .GreaterThanOrEqualTo(query => query.From!.Value)
            .When(query => query.From.HasValue && query.To.HasValue)
            .WithMessage("'To' değeri 'From' değerinden önce olamaz.");
    }
}
