using FluentValidation;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MedicalBookingSystem.Application.Validators
{
    public class CreateAppointmentDtoValidator : AbstractValidator<CreateAppointmentDto>
    {
        public CreateAppointmentDtoValidator()
        {
            RuleFor(a => a.DoctorId)
                .NotEmpty().WithMessage("Wybór lekarza jest wymagany")
                .GreaterThan(0).WithMessage("Nieprawidłowy identyfikator lekarza");

            RuleFor(a => a.Date)
                .NotEmpty().WithMessage("Data wizyty jest wymagana")
                .GreaterThan(DateTime.Now.AddHours(24)).WithMessage("Wizyta musi być zaplanowana z co najmniej 24-godzinnym wyprzedzeniem")
                .LessThan(DateTime.Now.AddMonths(3)).WithMessage("Nie można rezerwować wizyt z więcej niż 3-miesięcznym wyprzedzeniem");
        }
    }
}
