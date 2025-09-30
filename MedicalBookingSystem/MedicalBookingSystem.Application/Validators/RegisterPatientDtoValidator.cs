using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Validators
{
    public class RegisterPatientDtoValidator : AbstractValidator<RegisterPatientDto>
    {
        public RegisterPatientDtoValidator()
        {
            RuleFor(p => p.Email)
                .NotEmpty().WithMessage("Email jest wymagany")
                .EmailAddress().WithMessage("Nieprawidłowy format email")
                .MaximumLength(255);

            RuleFor(p => p.Password)
                .NotEmpty().WithMessage("Hasło jest wymagane")
                .MinimumLength(8).WithMessage("Hasło musi mieć minimum 8 znaków")
                .Matches("[A-Z]").WithMessage("Hasło musi zawierać przynajmniej 1 wielką literę")
                .Matches("[0-9]").WithMessage("Hasło musi zawierać przynajmniej 1 cyfrę");

            RuleFor(p => p.FirstName)
                .NotEmpty().WithMessage("Imię jest wymagane")
                .MaximumLength(50).WithMessage("Imię nie może przekraczać 50 znaków");

            RuleFor(p => p.LastName)
                .NotEmpty().WithMessage("Nazwisko jest wymagane")
                .MaximumLength(50).WithMessage("Nazwisko nie może przekraczać 50 znaków");

            RuleFor(p => p.DateOfBirth)
                .NotEmpty().WithMessage("Data urodzenia jest wymagana")
                .LessThan(DateTime.Now.AddYears(-18)).WithMessage("Pacjent musi mieć ukończone 18 lat");
        }
    }
}
