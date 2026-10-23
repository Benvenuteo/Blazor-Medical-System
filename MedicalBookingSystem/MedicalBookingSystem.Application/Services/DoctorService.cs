using MedicalBookingSystem.Application.Queries;
using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Application.Validators;
using FluentValidation;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Services
{
    public class DoctorService : IDoctorService
    {
        private readonly IUnitOfWorkFactory _units;
        private readonly IDoctorDirectoryQueries _directory;
        private readonly UserAccess _access;
        private readonly ClinicClock _clock;
        private readonly IValidator<CreateScheduleDto> _scheduleValidator;


        public DoctorService(IUnitOfWorkFactory units, IValidator<CreateScheduleDto> scheduleValidator, ClinicClock clock, UserAccess access, IDoctorDirectoryQueries directory)
        {
            _units = units;
            _directory = directory;
            _access = access;
            _clock = clock;
            _scheduleValidator = scheduleValidator;

        }

        public async Task<Result<DoctorDetailsDto>> GetDoctorDetailsAsync(int id)
        {
            var doctor = await _directory.GetDetailsAsync(id, _clock.Now, _clock.Now.AddMonths(3));
            return doctor is null ? Result.Failure<DoctorDetailsDto>("Lekarz nie został znaleziony.", ErrorType.NotFound) : Result.Success(doctor);
        }

        public async Task<Result<IEnumerable<DoctorScheduleDto>>> GetDoctorScheduleAsync(int doctorId)
        {
            using var _uow = _units.Create();
            if (!await _uow.Doctors.AnyAsync(d => d.Id == doctorId))
                return Result.Failure<IEnumerable<DoctorScheduleDto>>("Lekarz nie istnieje.", ErrorType.NotFound);
            var from = _clock.Now;
            var to = from.AddMonths(3);
            var schedules = await _uow.DoctorSchedules.GetInRangeAsync(doctorId, from, to);

            IEnumerable<DoctorScheduleDto> dto = schedules.Select(item => item.ToDto()).ToList();
            return Result<IEnumerable<DoctorScheduleDto>>.Success(dto);
        }

        public async Task<Result<PagedResult<DoctorDto>>> SearchDoctorsAsync(SearchDoctorsDto dto)
        {
            var validation = await new SearchDoctorsDtoValidator().ValidateAsync(dto);
            if (!validation.IsValid) return Result.Failure<PagedResult<DoctorDto>>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
            var from = AppointmentSlots.RoundUp(_clock.MinimumBookingTime);
            var to = _clock.Now.AddMonths(3);
            if (dto.Date.HasValue)
            {
                var day = DateTime.SpecifyKind(dto.Date.Value.Date, DateTimeKind.Unspecified);
                if (day > from) from = day;
                if (day.AddDays(1).AddTicks(-1) < to) to = day.AddDays(1).AddTicks(-1);
            }
            var starts = new List<DateTime>();
            for (var start = from; start <= to; start = start.Add(AppointmentSlots.Duration))
                if (_clock.IsUnambiguousLocalTime(start) && _clock.IsUnambiguousLocalTime(start.Add(AppointmentSlots.Duration))) starts.Add(start);
            return Result.Success(await _directory.SearchAsync(dto, starts));
        }

        public async Task<Result<IReadOnlyList<AvailableSlotDto>>> GetAvailableSlotsAsync(int doctorId,
            DateTime? date = null, int? excludedAppointmentId = null)
        {
            if (date.HasValue && (date.Value.Kind != DateTimeKind.Unspecified || date.Value.TimeOfDay != TimeSpan.Zero || date.Value.Date == DateTime.MaxValue.Date))
                return Result.Failure<IReadOnlyList<AvailableSlotDto>>("Podaj poprawny lokalny dzień bez godziny.");
            using var _uow = _units.Create();
            if (!await _uow.Doctors.AnyAsync(d => d.Id == doctorId))
                return Result.Failure<IReadOnlyList<AvailableSlotDto>>("Lekarz nie istnieje.", ErrorType.NotFound);
            if (excludedAppointmentId.HasValue)
            {
                var appointment = await _uow.Appointments.GetByIdAsync(excludedAppointmentId.Value);
                if (appointment is null) return Result.Failure<IReadOnlyList<AvailableSlotDto>>("Wizyta nie istnieje.", ErrorType.NotFound);
                await _access.RequirePatientAsync(appointment.PatientId);
                if (appointment.DoctorId != doctorId)
                    return Result.Failure<IReadOnlyList<AvailableSlotDto>>("Wizyta jest przypisana do innego lekarza.");
            }
            var schedules = await _uow.DoctorSchedules.GetInRangeAsync(doctorId, _clock.MinimumBookingTime, _clock.Now.AddMonths(3));
            var lower = _clock.MinimumBookingTime.Subtract(AppointmentSlots.Duration);
            var upper = _clock.Now.AddMonths(3).Add(AppointmentSlots.Duration);
            var patientId = await _access.PatientIdOrNullAsync();
            var appointments = await _uow.Appointments.FindAsync(a => (a.DoctorId == doctorId || a.PatientId == patientId) &&
                a.Id != excludedAppointmentId && a.Status != AppointmentStatus.Cancelled && a.Date >= lower && a.Date <= upper);
            IReadOnlyList<AvailableSlotDto> result = AvailableStarts(schedules, appointments, date)
                .Select(start => new AvailableSlotDto { DoctorId = doctorId, StartTime = start, EndTime = start.Add(AppointmentSlots.Duration) }).ToList();
            return Result.Success(result);
        }

        private IReadOnlyList<DateTime> AvailableStarts(IEnumerable<DoctorSchedule> schedules, IEnumerable<Appointment> appointments, DateTime? date)
        {
            var from = _clock.MinimumBookingTime;
            var to = _clock.Now.AddMonths(3);
            if (date.HasValue)
            {
                var day = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Unspecified);
                if (day > to || day.AddDays(1) <= from) return [];
                if (day > from) from = day;
                var end = day.AddDays(1).AddTicks(-1);
                if (end < to) to = end;
            }
            return AppointmentSlots.GetAvailableStarts(schedules, appointments, from, to)
                .Where(start => _clock.IsUnambiguousLocalTime(start) && _clock.IsUnambiguousLocalTime(start.Add(AppointmentSlots.Duration))).ToList();
        }

        public async Task<Result<IEnumerable<SpecializationDto>>> GetSpecializationsAsync()
        {
            using var _uow = _units.Create();
            var specializations = await _uow.Specializations.GetAllAsync();
            IEnumerable<SpecializationDto> dto = specializations.Select(item => item.ToDto()).ToList();
            return Result<IEnumerable<SpecializationDto>>.Success(dto);
        }

        public async Task<Result> CreateScheduleAsync(CreateScheduleDto dto)
        {
            using var _uow = _units.Create();
            await _access.RequireScheduleManagementAsync(dto.DoctorId);
            var validationResult = await _scheduleValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return Result.Failure(string.Join("; ", validationResult.Errors.Select(error => error.ErrorMessage)));

            if (await _uow.Doctors.GetByIdAsync(dto.DoctorId) is null)
                return Result.Failure("Lekarz nie istnieje.");

            var schedule = new DoctorSchedule
            {
                DoctorId = dto.DoctorId,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                IsAvailable = true
            };

            await _uow.DoctorSchedules.AddAsync(schedule);
            try { await _uow.SaveChangesAsync(); }
            catch (PersistenceConflictException)
            {
                return Result.Failure("Grafik nachodzi na przedział zapisany w innym żądaniu.", ErrorType.Conflict);
            }
            return Result.Success();
        }

    }
}
