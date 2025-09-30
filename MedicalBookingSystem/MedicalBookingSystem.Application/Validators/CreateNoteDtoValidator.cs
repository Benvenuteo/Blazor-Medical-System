using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto.NotesPrescriptionDto;

namespace MedicalBookingSystem.Application.Validators
{
    public class CreateNoteDtoValidator : AbstractValidator<CreateNoteDto>
    {
        public CreateNoteDtoValidator()
        {
            RuleFor(n => n.AppointmentId)
                .NotEmpty().WithMessage("Identyfikator wizyty jest wymagany")
                .GreaterThan(0).WithMessage("Nieprawidłowy identyfikator wizyty");

            RuleFor(n => n.Content)
                .NotEmpty().WithMessage("Treść notatki jest wymagana")
                .MinimumLength(10).WithMessage("Notatka musi zawierać minimum 10 znaków")
                .MaximumLength(2000).WithMessage("Notatka nie może przekraczać 2000 znaków");
        }
    }
}
