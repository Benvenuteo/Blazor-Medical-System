using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Validators;

public sealed class ForgotPasswordDtoValidator : AbstractValidator<ForgotPasswordDto>
{
    public ForgotPasswordDtoValidator() => RuleFor(d => d.Email)
        .NotEmpty().WithMessage("Email jest wymagany").EmailAddress().WithMessage("Nieprawidłowy format email").MaximumLength(255);
}

public sealed class ResetPasswordDtoValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordDtoValidator()
    {
        RuleFor(d => d.Token).NotEmpty().WithMessage("Link jest niekompletny.").MaximumLength(100);
        RuleFor(d => d.NewPassword).NotEmpty().WithMessage("Hasło jest wymagane").NewPatientPassword()
            .Equal(d => d.ConfirmNewPassword).WithMessage("Hasła nie są identyczne");
    }
}
