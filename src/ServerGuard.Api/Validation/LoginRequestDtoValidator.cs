using FluentValidation;
using ServerGuard.Shared;
using ServerGuard.Shared.Dtos;

namespace ServerGuard.Api.Validation;

/// <summary>
/// Giriş isteğinin biçimsel doğrulaması. Kimlik bilgilerinin doğruluğu burada değil,
/// <see cref="Security.IPanelAuthenticator"/> içinde kontrol edilir.
/// </summary>
public sealed class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(request => request.UserName)
            .NotEmpty()
            .Length(AuthConstraints.MinimumUserNameLength, AuthConstraints.MaximumUserNameLength);

        RuleFor(request => request.Password)
            .NotEmpty()
            .Length(AuthConstraints.MinimumPasswordLength, AuthConstraints.MaximumPasswordLength);
    }
}
