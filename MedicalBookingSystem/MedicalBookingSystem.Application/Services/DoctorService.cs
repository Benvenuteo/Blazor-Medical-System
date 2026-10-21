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
        private readonly IUnitOfWork _uow;
        private readonly UserAccess _access;
        private readonly ClinicClock _clock;
        private readonly IValidator<CreateScheduleDto> _scheduleValidator;


        public DoctorService(IUnitOfWork uow, IValidator<CreateScheduleDto> scheduleValidator, ClinicClock clock, UserAccess access)
        {
            _uow = uow;
            _access = access;
            _clock = clock;
            _scheduleValidator = scheduleValidator;

        }

        public async Task<Result<DoctorDetailsDto>> GetDoctorDetailsAsync(int id)
        {
            var doctor = await _uow.Doctors.GetByIdAsync(id);
            if (doctor == null)
                return Result<DoctorDetailsDto>.Failure<DoctorDetailsDto>("Lekarz nie został znaleziony.");

            var dto = doctor.ToDetailsDto();
            return Result<DoctorDetailsDto>.Success(dto);
        }

        public async Task<Result<DoctorRatingDto>> GetDoctorRatingAsync(int doctorId)
        {
            var doctor = await _uow.Doctors.GetByIdWithReviewsAsync(doctorId);

            if (doctor == null)
                return Result<DoctorRatingDto>.Failure<DoctorRatingDto>("Lekarz nie został znaleziony.");

            var averageRating = await _uow.Doctors.GetAverageRatingAsync(doctorId);
            var totalReviews = doctor.Reviews?.Count ?? 0;

            var dto = new DoctorRatingDto
            {
                AverageRating = Math.Round(averageRating, 2),
                TotalReviews = totalReviews
            };

            return Result<DoctorRatingDto>.Success(dto);
        }


        public async Task<Result<IEnumerable<DoctorScheduleDto>>> GetDoctorScheduleAsync(int doctorId)
        {
            var schedules = await _uow.DoctorSchedules.GetByDoctorIdAsync(doctorId);
            if (schedules == null)
                return Result<IEnumerable<DoctorScheduleDto>>.Failure<IEnumerable<DoctorScheduleDto>>("Nie znaleziono harmonogramu.");

            IEnumerable<DoctorScheduleDto> dto = schedules.Select(item => item.ToDto()).ToList();
            return Result<IEnumerable<DoctorScheduleDto>>.Success(dto);
        }

        public async Task<Result<IEnumerable<DoctorDto>>> SearchDoctorsAsync(SearchDoctorsDto dto)
        {
            IEnumerable<Doctor> doctors = await _uow.Doctors.GetAllAsync();
            if (dto.SpecializationId.HasValue)
                doctors = doctors.Where(d => d.DoctorSpecializations.Any(s => s.SpecializationId == dto.SpecializationId.Value));
            if (!string.IsNullOrWhiteSpace(dto.Region))
                doctors = doctors.Where(d => string.Equals(d.Region, dto.Region.Trim(), StringComparison.OrdinalIgnoreCase));
            var mapped = doctors.Select(d => MapWithAvailability(d, dto.Date)).ToList();
            if (dto.Date.HasValue) mapped = mapped.Where(d => d.NextAvailableSlot.HasValue).ToList();
            IEnumerable<DoctorDto> ordered = dto.SortBy?.ToLowerInvariant() switch
            {
                "rating" => mapped.OrderByDescending(d => d.AverageRating).ThenBy(d => d.Id),
                "availability" => mapped.OrderBy(d => d.NextAvailableSlot ?? DateTime.MaxValue).ThenBy(d => d.Id),
                _ => mapped.OrderBy(d => d.Id)
            };
            return Result.Success(ordered);
        }

        public async Task<Result<IEnumerable<DoctorDto>>> GetAllAsync() => Result.Success(
            (await _uow.Doctors.GetAllAsync()).Select(d => MapWithAvailability(d, null)));

        public async Task<Result<IReadOnlyList<AvailableSlotDto>>> GetAvailableSlotsAsync(int doctorId,
            DateTime? date = null, int? excludedAppointmentId = null)
        {
            if (await _uow.Doctors.GetByIdAsync(doctorId) is null)
                return Result.Failure<IReadOnlyList<AvailableSlotDto>>("Lekarz nie istnieje.", ErrorType.NotFound);
            if (excludedAppointmentId.HasValue)
            {
                var appointment = await _uow.Appointments.GetByIdAsync(excludedAppointmentId.Value);
                if (appointment is null) return Result.Failure<IReadOnlyList<AvailableSlotDto>>("Wizyta nie istnieje.", ErrorType.NotFound);
                await _access.RequirePatientAsync(appointment.PatientId);
                if (appointment.DoctorId != doctorId)
                    return Result.Failure<IReadOnlyList<AvailableSlotDto>>("Wizyta jest przypisana do innego lekarza.");
            }
            var schedules = await _uow.DoctorSchedules.GetByDoctorIdAsync(doctorId);
            var lower = _clock.MinimumBookingTime.Subtract(AppointmentSlots.Duration);
            var upper = _clock.Now.AddMonths(3).Add(AppointmentSlots.Duration);
            var patientId = await _access.PatientIdOrNullAsync();
            var appointments = await _uow.Appointments.FindAsync(a => (a.DoctorId == doctorId || a.PatientId == patientId) &&
                a.Id != excludedAppointmentId && a.Status != AppointmentStatus.Cancelled && a.Date >= lower && a.Date <= upper);
            IReadOnlyList<AvailableSlotDto> result = AvailableStarts(schedules, appointments, date)
                .Select(start => new AvailableSlotDto { DoctorId = doctorId, StartTime = start, EndTime = start.Add(AppointmentSlots.Duration) }).ToList();
            return Result.Success(result);
        }

        private DoctorDto MapWithAvailability(Doctor doctor, DateTime? date)
        {
            var dto = doctor.ToDto();
            var starts = AvailableStarts(doctor.Schedules, doctor.Appointments, date);
            dto.NextAvailableSlot = starts.Count > 0 ? starts[0] : null;
            return dto;
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
            var specializations = await _uow.Specializations.GetAllAsync();
            IEnumerable<SpecializationDto> dto = specializations.Select(item => item.ToDto()).ToList();
            return Result<IEnumerable<SpecializationDto>>.Success(dto);
        }

        public async Task<bool> SetDoctorImageUrlAsync(int doctorId, string imageUrl)
        {
            await _access.RequireAdminAsync();
            var doctor = await _uow.Doctors.GetByIdAsync(doctorId);
            if (doctor == null) return false;

            doctor.ImageUrl = imageUrl;
            await _uow.SaveChangesAsync();
            return true;
        }

        public async Task<Result> CreateScheduleAsync(CreateScheduleDto dto)
        {
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
