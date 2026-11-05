using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Validators;

public sealed class SaveClinicDtoValidator : AbstractValidator<SaveClinicDto>
{
    public SaveClinicDtoValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Nazwa placówki jest wymagana").MaximumLength(150);
        RuleFor(c => c.Street).NotEmpty().WithMessage("Ulica jest wymagana").MaximumLength(200);
        RuleFor(c => c.City).NotEmpty().WithMessage("Miejscowość jest wymagana").MaximumLength(100);
        RuleFor(c => c.PostalCode).Matches(@"^\d{2}-\d{3}$").WithMessage("Kod pocztowy w formacie 00-000")
            .When(c => !string.IsNullOrWhiteSpace(c.PostalCode));
        RuleFor(c => c.Phone).PatientPhoneNumber().When(c => !string.IsNullOrWhiteSpace(c.Phone));
        RuleFor(c => c.Latitude).NotNull().WithMessage("Ustaw położenie placówki na mapie").InclusiveBetween(-90, 90);
        RuleFor(c => c.Longitude).NotNull().WithMessage("Ustaw położenie placówki na mapie").InclusiveBetween(-180, 180);
        RuleFor(c => c.DoctorIds).Must(ids => ids.Count <= 200).WithMessage("Zbyt wielu lekarzy w jednym żądaniu.");
    }
}
