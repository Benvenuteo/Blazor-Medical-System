using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;

namespace MedicalBookingSystem.Application.Validators
{
    public class CreatePrescriptionDtoValidator : AbstractValidator<CreatePrescriptionDto>
    {
        public CreatePrescriptionDtoValidator()
        {
            RuleFor(p => p.AppointmentId)
                .NotEmpty().WithMessage("Identyfikator wizyty jest wymagany")
                .GreaterThan(0).WithMessage("Nieprawidłowy identyfikator wizyty");

            RuleFor(p => p.Medication)
                .NotEmpty().WithMessage("Nazwa leku jest wymagana")
                .MaximumLength(100).WithMessage("Nazwa leku nie może przekraczać 100 znaków");

            RuleFor(p => p.Dosage)
                .NotEmpty().WithMessage("Dawkowanie jest wymagane")
                .MaximumLength(50).WithMessage("Dawkowanie nie może przekraczać 50 znaków");

            RuleFor(p => p.ValidityDays)
                .InclusiveBetween(1, 365).WithMessage("Okres ważności musi być między 1 a 365 dni");
        }
    }
}
