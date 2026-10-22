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

            RuleFor(s => s.SortBy)
                .Must(BeValidSortOption).WithMessage("Nieprawidłowa opcja sortowania")
                .When(s => !string.IsNullOrEmpty(s.SortBy));
        }

        private bool BeValidSortOption(string? sortBy)
        {
            var validOptions = new[] { "rating", "availability" };
            return sortBy is not null && validOptions.Contains(sortBy.ToLowerInvariant());
        }
    }
}
