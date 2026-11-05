using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;

namespace MedicalBookingSystem.Application.Validators
{
    public class SearchDoctorsDtoValidator : AbstractValidator<SearchDoctorsDto>
    {
        public SearchDoctorsDtoValidator()
        {
            RuleFor(s => s.Page).InclusiveBetween(1, 100000);
            RuleFor(s => s.PageSize).InclusiveBetween(1, 50);
            RuleFor(s => s.Date).Must(d => !d.HasValue || (d.Value.Kind == DateTimeKind.Unspecified && d.Value.TimeOfDay == TimeSpan.Zero && d.Value.Date < DateTime.MaxValue.Date))
                .WithMessage("Filtr daty musi wskazywać dzień, bez godziny.");
            RuleFor(s => s.SpecializationId)
                .GreaterThan(0).WithMessage("Nieprawidłowy identyfikator specjalizacji")
                .When(s => s.SpecializationId.HasValue);

            RuleFor(s => s.Region)
                .MaximumLength(100).WithMessage("Nazwa regionu nie może przekraczać 100 znaków")
                .When(s => !string.IsNullOrEmpty(s.Region));

            RuleFor(s => s.Latitude).InclusiveBetween(-90, 90).When(s => s.Latitude.HasValue);
            RuleFor(s => s.Longitude).InclusiveBetween(-180, 180).When(s => s.Longitude.HasValue);
            RuleFor(s => s).Must(s => s.Latitude.HasValue == s.Longitude.HasValue)
                .WithMessage("Podaj obie współrzędne punktu wyszukiwania albo żadną.");
            RuleFor(s => s.RadiusKm).InclusiveBetween(1, MedicalBookingSystem.SharedKernel.Dto.MapDefaults.MaxRadiusKm)
                .WithMessage("Promień musi mieścić się w zakresie 1–100 km.").When(s => s.RadiusKm.HasValue);
            RuleFor(s => s.SortBy).Must(s => s?.ToLowerInvariant() != "distance").When(s => !s.HasLocation)
                .WithMessage("Sortowanie po odległości wymaga punktu wyszukiwania.");

            RuleFor(s => s.SortBy)
                .Must(BeValidSortOption).WithMessage("Nieprawidłowa opcja sortowania")
                .When(s => !string.IsNullOrEmpty(s.SortBy));
        }

        private bool BeValidSortOption(string? sortBy)
        {
            var validOptions = new[] { "rating", "availability", "distance" };
            return sortBy is not null && validOptions.Contains(sortBy.ToLowerInvariant());
        }
    }
}
