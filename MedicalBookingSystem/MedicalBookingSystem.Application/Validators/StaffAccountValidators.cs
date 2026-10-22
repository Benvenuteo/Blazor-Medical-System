using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using MedicalBookingSystem.SharedKernel.Security;

namespace MedicalBookingSystem.Application.Validators;

internal static class StaffPasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongStaffPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .NotEmpty().WithMessage("Hasło jest wymagane.")
        .MinimumLength(12).WithMessage("Hasło personelu musi mieć co najmniej 12 znaków.")
        .MaximumLength(128).WithMessage("Hasło może mieć najwyżej 128 znaków.")
        .Matches("[A-Z]").WithMessage("Hasło musi zawierać wielką literę.")
        .Matches("[0-9]").WithMessage("Hasło musi zawierać cyfrę.");
}

public sealed class CreateStaffAccountValidator : AbstractValidator<CreateStaffAccountDto>
{
    public CreateStaffAccountValidator()
    {
        RuleFor(d => d.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(d => d.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(d => d.Role).Must(r => r is Roles.Doctor or Roles.Admin).WithMessage("Wybierz rolę lekarza lub administratora.");
        RuleFor(d => d.DoctorId).NotNull().GreaterThan(0).When(d => d.Role == Roles.Doctor).WithMessage("Wybierz lekarza.");
        RuleFor(d => d.DoctorId).Null().When(d => d.Role == Roles.Admin).WithMessage("Administrator nie może mieć przypisanego lekarza.");
        RuleFor(d => d.InitialPassword).StrongStaffPassword();
    }
}

public sealed class UpdateStaffAccountValidator : AbstractValidator<UpdateStaffAccountDto>
{
    public UpdateStaffAccountValidator()
    {
        RuleFor(d => d.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(d => d.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(d => d.Version).NotEmpty();
    }
}

public sealed class ResetStaffPasswordValidator : AbstractValidator<ResetStaffPasswordDto>
{
    public ResetStaffPasswordValidator()
    {
        RuleFor(d => d.NewPassword).StrongStaffPassword();
        RuleFor(d => d.Version).NotEmpty();
    }
}

public sealed class ChangeStaffPasswordValidator : AbstractValidator<ChangeStaffPasswordDto>
{
    public ChangeStaffPasswordValidator()
    {
        RuleFor(d => d.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(d => d.NewPassword).StrongStaffPassword();
        RuleFor(d => d.ConfirmNewPassword).Equal(d => d.NewPassword).WithMessage("Nowe hasła nie są identyczne.");
    }
}

public sealed class AccountSearchValidator : AbstractValidator<AccountSearchDto>
{
    public AccountSearchValidator()
    {
        RuleFor(d => d.Page).InclusiveBetween(1, 100000);
        RuleFor(d => d.PageSize).InclusiveBetween(1, 50);
        RuleFor(d => d.Search).MaximumLength(200);
    }
}
