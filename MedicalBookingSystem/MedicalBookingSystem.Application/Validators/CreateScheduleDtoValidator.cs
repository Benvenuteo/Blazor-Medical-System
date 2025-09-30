using FluentValidation;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.SharedKernel.Dto;
namespace MedicalBookingSystem.Application.Validators
{
    public class CreateScheduleDtoValidator : AbstractValidator<CreateScheduleDto>
    {
        private readonly IUnitOfWork _uow;

        public CreateScheduleDtoValidator(IUnitOfWork uow)
        {
            _uow = uow;

            RuleFor(x => x.DoctorId)
                .GreaterThan(0).WithMessage("Wybierz lekarza.");

            RuleFor(x => x.StartTime)
                .LessThan(x => x.EndTime).WithMessage("Godzina rozpoczęcia musi być wcześniejsza niż zakończenia.");

            RuleFor(x => x.EndTime)
                .GreaterThan(DateTime.Now).WithMessage("Nie można dodać przeszłego przedziału.");

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
