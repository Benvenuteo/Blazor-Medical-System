using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.Application.Time;
using FluentValidation;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.SharedKernel.Dto;
namespace MedicalBookingSystem.Application.Validators
{
    public class CreateScheduleDtoValidator : AbstractValidator<CreateScheduleDto>
    {
        private readonly IUnitOfWork _uow;

        public CreateScheduleDtoValidator(IUnitOfWork uow, ClinicClock clock)
        {
            _uow = uow;

            RuleFor(x => x.DoctorId)
                .GreaterThan(0).WithMessage("Wybierz lekarza.");

            RuleFor(x => x.StartTime)
                .GreaterThanOrEqualTo(_ => clock.Now).WithMessage("Grafik nie może rozpoczynać się w przeszłości.")
                .LessThan(x => x.EndTime).WithMessage("Godzina rozpoczęcia musi być wcześniejsza niż zakończenia.")
                .Must(date => date.Kind == DateTimeKind.Unspecified && clock.IsUnambiguousLocalTime(date))
                .WithMessage("Początek grafiku musi być jednoznacznym lokalnym czasem placówki.");

            RuleFor(x => x.EndTime)
                .GreaterThan(_ => clock.Now).WithMessage("Nie można dodać przeszłego przedziału.")
                .Must((dto, end) => end - dto.StartTime >= AppointmentSlots.Duration)
                .WithMessage("Grafik musi obejmować co najmniej 30 minut.")
                .Must(date => date.Kind == DateTimeKind.Unspecified && clock.IsUnambiguousLocalTime(date))
                .WithMessage("Koniec grafiku musi być jednoznacznym lokalnym czasem placówki.");

            RuleFor(x => x)
                .Must(dto => dto.StartTime.Ticks <= DateTime.MaxValue.Ticks - AppointmentSlots.Duration.Ticks &&
                    dto.EndTime.Ticks >= AppointmentSlots.Duration.Ticks &&
                    AppointmentSlots.RoundUp(dto.StartTime) <= dto.EndTime.Subtract(AppointmentSlots.Duration))
                .WithMessage("Grafik musi obejmować co najmniej jeden pełny termin 30-minutowy.");

            RuleFor(x => x)
                .MustAsync(NotOverlapWithExistingSchedules)
                .WithMessage("Podany przedział czasowy nachodzi na istniejący grafik.");
        }

        private async Task<bool> NotOverlapWithExistingSchedules(CreateScheduleDto dto, CancellationToken _)
        {
            var schedules = await _uow.DoctorSchedules.GetByDoctorIdAsync(dto.DoctorId);

            return schedules.All(s =>
                dto.EndTime <= s.StartTime || dto.StartTime >= s.EndTime
            );
        }
    }
}
