using FluentValidation;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Validators;

/// <summary>The same requirements as registration: adult patient and a valid phone number.</summary>
public sealed class CompleteProfileDtoValidator : AbstractValidator<CompleteProfileDto>
{
    public CompleteProfileDtoValidator(ClinicClock clock)
    {
        RuleFor(p => p.FirstName).NotEmpty().WithMessage("Imię jest wymagane").MaximumLength(50);
        RuleFor(p => p.LastName).NotEmpty().WithMessage("Nazwisko jest wymagane").MaximumLength(50);
        RuleFor(p => p.DateOfBirth)
            .NotNull().WithMessage("Data urodzenia jest wymagana")
            .Must(value => value!.Value.Date <= clock.Today.AddYears(-18)).WithMessage("Pacjent musi mieć ukończone 18 lat")
            .When(p => p.DateOfBirth is not null, ApplyConditionTo.CurrentValidator);
        RuleFor(p => p.PhoneNumber).NotEmpty().WithMessage("Numer telefonu jest wymagany").PatientPhoneNumber();
    }
}
