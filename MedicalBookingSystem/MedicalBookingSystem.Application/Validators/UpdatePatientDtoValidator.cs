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
                .MaximumLength(50).WithMessage("Imię nie może przekraczać 50 znaków");

            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("Nazwisko jest wymagane")
                .MaximumLength(50).WithMessage("Nazwisko nie może przekraczać 50 znaków");

            RuleFor(x => x.PhoneNumber)
                .Matches(@"^\+?[0-9\s-]{9,}$").WithMessage("Nieprawidłowy format numeru telefonu")
                .When(x => !string.IsNullOrEmpty(x.PhoneNumber));

            RuleFor(x => x.CurrentPassword)
                .NotEmpty().WithMessage("Aktualne hasło jest wymagane do zmiany danych")
                .When(x => !string.IsNullOrEmpty(x.NewPassword));

            RuleFor(x => x.NewPassword)
                .MinimumLength(8).WithMessage("Nowe hasło musi mieć minimum 8 znaków")
                .Matches("[A-Z]").WithMessage("Nowe hasło musi zawierać przynajmniej 1 wielką literę")
                .Matches("[0-9]").WithMessage("Nowe hasło musi zawierać przynajmniej 1 cyfrę")
                .Equal(x => x.ConfirmNewPassword).WithMessage("Hasła nie są identyczne")
                .When(x => !string.IsNullOrEmpty(x.NewPassword));

            RuleFor(x => x.Address)
                .MaximumLength(200).WithMessage("Adres nie może przekraczać 200 znaków");
        }
    }
}
