using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Validators
{
    public class UpdatePatientDtoValidator : AbstractValidator<UpdatePatientDto>
    {
        public UpdatePatientDtoValidator()
        {
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("Imię jest wymagane")
                .MaximumLength(50).WithMessage("Imię nie może przekraczać 50 znaków")
                .When(x => x.FirstName is not null);

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Nazwisko jest wymagane")
                .MaximumLength(50).WithMessage("Nazwisko nie może przekraczać 50 znaków")
                .When(x => x.LastName is not null);

            RuleFor(x => x.PhoneNumber)
                .PatientPhoneNumber()
                .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

            RuleFor(x => x.CurrentPassword)
                .MaximumLength(PatientRules.PasswordMaxLength).WithMessage($"Hasło może mieć najwyżej {PatientRules.PasswordMaxLength} znaków")
                .NotEmpty().WithMessage("Aktualne hasło jest wymagane do zmiany danych")
                .When(x => !string.IsNullOrEmpty(x.NewPassword));

            RuleFor(x => x.NewPassword)
                .NewPatientPassword()
                .Equal(x => x.ConfirmNewPassword).WithMessage("Hasła nie są identyczne")
                .When(x => !string.IsNullOrEmpty(x.NewPassword));
        }
    }
}
