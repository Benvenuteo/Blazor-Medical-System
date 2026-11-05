using FluentValidation;

namespace MedicalBookingSystem.Application.Validators;

/// <summary>Rules shared by patient registration and profile editing, so both paths accept the same data.</summary>
public static class PatientRules
{
    public const int PasswordMaxLength = 128;
    public const int PhoneMaxLength = 20;

    public static IRuleBuilderOptions<T, string?> PatientPhoneNumber<T>(this IRuleBuilder<T, string?> rule) => rule
        .MaximumLength(PhoneMaxLength).WithMessage($"Numer telefonu może mieć najwyżej {PhoneMaxLength} znaków")
        .Matches(@"^\+?[0-9][0-9\s-]{7,}[0-9]$").WithMessage("Nieprawidłowy format numeru telefonu")
        .Must(phone => phone!.Count(char.IsAsciiDigit) >= 9).WithMessage("Numer telefonu musi mieć co najmniej 9 cyfr");

    public static IRuleBuilderOptions<T, string> NewPatientPassword<T>(this IRuleBuilder<T, string> rule) => rule
        .MaximumLength(PasswordMaxLength).WithMessage($"Hasło może mieć najwyżej {PasswordMaxLength} znaków")
        .MinimumLength(8).WithMessage("Hasło musi mieć minimum 8 znaków")
        .Matches("[A-Z]").WithMessage("Hasło musi zawierać przynajmniej 1 wielką literę")
        .Matches("[0-9]").WithMessage("Hasło musi zawierać przynajmniej 1 cyfrę");
}
