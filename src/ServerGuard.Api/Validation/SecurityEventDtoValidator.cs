using FluentValidation;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Validation;

public sealed class SecurityEventDtoValidator : AbstractValidator<SecurityEventDto>
{
    public SecurityEventDtoValidator()
    {
        RuleFor(dto => dto.ServerName)
            .NotEmpty()
            .MaximumLength(ServerConstraints.NameMaxLength);

        RuleFor(dto => dto.EventType)
            .IsInEnum();

        RuleFor(dto => dto.SourceIp)
            .NotEmpty()
            .MaximumLength(SecurityEventConstraints.SourceIpMaxLength);

        RuleFor(dto => dto.Username)
            .NotEmpty()
            .MaximumLength(SecurityEventConstraints.UsernameMaxLength);
    }
}
