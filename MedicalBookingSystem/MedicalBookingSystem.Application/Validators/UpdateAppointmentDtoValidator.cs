using MedicalBookingSystem.Application.Time;
using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;

namespace MedicalBookingSystem.Application.Validators
{
    public class UpdateAppointmentDtoValidator : AbstractValidator<UpdateAppointmentDto>
    {
        public UpdateAppointmentDtoValidator(ClinicClock clock)
        {
            RuleFor(a => a.NewDate)
                .NotEmpty().WithMessage("Nowa data wizyty jest wymagana")
                .GreaterThanOrEqualTo(_ => clock.Now.AddHours(24)).WithMessage("Wizyta musi być zaplanowana z co najmniej 24-godzinnym wyprzedzeniem");
        }
    }
}
