using FluentValidation;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;

namespace MedicalBookingSystem.Application.Validators;

public sealed class CreateAppointmentDtoValidator : AbstractValidator<CreateAppointmentDto>
{
    public CreateAppointmentDtoValidator(ClinicClock clock)
    {
        RuleFor(dto => dto.DoctorId).GreaterThan(0).WithMessage("Wybierz lekarza.");
        RuleFor(dto => dto.Date).Cascade(CascadeMode.Stop).BookableClinicTime(clock);
    }
}
