using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Validators
{
    public class CreateReviewDtoValidator : AbstractValidator<CreateReviewDto>
    {
        public CreateReviewDtoValidator()
        {
            RuleFor(r => r.DoctorId)
                .NotEmpty().WithMessage("Wybór lekarza jest wymagany")
                .GreaterThan(0).WithMessage("Nieprawidłowy identyfikator lekarza");

            RuleFor(r => r.Rating)
                .NotEmpty().WithMessage("Ocena jest wymagana")
                .InclusiveBetween(1, 5).WithMessage("Ocena musi być w zakresie 1-5");

            RuleFor(r => r.Comment)
                .MaximumLength(2000).WithMessage("Komentarz nie może przekraczać 2000 znaków")
                .When(r => !string.IsNullOrEmpty(r.Comment));
        }
    }
}
