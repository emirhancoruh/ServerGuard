using FluentValidation;
using ServerGuard.Api.Contracts;
using ServerGuard.Shared;

namespace ServerGuard.Api.Validation;

public sealed class ReportSummaryQueryValidator : AbstractValidator<ReportSummaryQuery>
{
    public ReportSummaryQueryValidator()
    {
        RuleFor(query => query.ServerName)
            .MaximumLength(ServerConstraints.NameMaxLength);

        When(query => query.From.HasValue && query.To.HasValue, () =>
        {
            RuleFor(query => query.To)
                .GreaterThanOrEqualTo(query => query.From!.Value)
                .WithMessage("'To' değeri 'From' değerinden önce olamaz.");

            RuleFor(query => query)
                .Must(query => (query.To!.Value - query.From!.Value).TotalDays <= ReportQueryConstraints.MaxRangeDays)
                .WithMessage($"Tarih aralığı en fazla {ReportQueryConstraints.MaxRangeDays} gün olabilir.")
                .OverridePropertyName(nameof(ReportSummaryQuery.From));
        });
    }
}
