using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto;

namespace MedicalBookingSystem.Application.Validators;

public sealed class PageQueryValidator : AbstractValidator<PageQueryDto>
{
    public PageQueryValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, 100000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 50);
    }
}

public sealed class AppointmentListQueryValidator : AbstractValidator<AppointmentListQueryDto>
{
    public AppointmentListQueryValidator()
    {
        Include(new PageQueryValidator());
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From).When(q => q.From.HasValue && q.To.HasValue);
        RuleFor(q => q.MissingDocument).Must(v => v is null or "Note" or "Prescription");
        RuleFor(q => q.From).Must(v => v is null || v.Value.Kind == DateTimeKind.Unspecified);
        RuleFor(q => q.To).Must(v => v is null || v.Value.Kind == DateTimeKind.Unspecified);
    }
}
