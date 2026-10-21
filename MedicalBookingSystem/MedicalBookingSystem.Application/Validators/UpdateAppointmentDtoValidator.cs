using FluentValidation;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;

namespace MedicalBookingSystem.Application.Validators;

public sealed class UpdateAppointmentDtoValidator : AbstractValidator<UpdateAppointmentDto>
{
    public UpdateAppointmentDtoValidator(ClinicClock clock) =>
        RuleFor(dto => dto.NewDate).Cascade(CascadeMode.Stop).BookableClinicTime(clock);
}
