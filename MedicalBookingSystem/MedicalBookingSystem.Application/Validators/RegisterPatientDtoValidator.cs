using MedicalBookingSystem.Application.Time;
using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Validators
{
    public class RegisterPatientDtoValidator : AbstractValidator<RegisterPatientDto>
    {
        public RegisterPatientDtoValidator(ClinicClock clock)
        {
            RuleFor(p => p.Email)
                .NotEmpty().WithMessage("Email jest wymagany")
                .EmailAddress().WithMessage("Nieprawidłowy format email")
                .MaximumLength(255);

            RuleFor(p => p.Password)
                .NotEmpty().WithMessage("Hasło jest wymagane")
                .NewPatientPassword();

            RuleFor(p => p.PhoneNumber)
                .PatientPhoneNumber()
                .When(p => !string.IsNullOrEmpty(p.PhoneNumber));

            RuleFor(p => p.FirstName)
                .NotEmpty().WithMessage("Imię jest wymagane")
                .MaximumLength(50).WithMessage("Imię nie może przekraczać 50 znaków");

            RuleFor(p => p.LastName)
                .NotEmpty().WithMessage("Nazwisko jest wymagane")
                .MaximumLength(50).WithMessage("Nazwisko nie może przekraczać 50 znaków");

            RuleFor(p => p.DateOfBirth)
                .NotEmpty().WithMessage("Data urodzenia jest wymagana")
                .Must(value => value.Date <= clock.Today.AddYears(-18)).WithMessage("Pacjent musi mieć ukończone 18 lat");
        }
    }
}
