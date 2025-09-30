using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;

namespace MedicalBookingSystem.Application.Validators
{
    public class SearchDoctorsDtoValidator : AbstractValidator<SearchDoctorsDto>
    {
        public SearchDoctorsDtoValidator()
        {
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

        private bool BeValidSortOption(string sortBy)
        {
            var validOptions = new[] { "rating", "distance", "availability" };
            return validOptions.Contains(sortBy.ToLower());
        }
    }
}
