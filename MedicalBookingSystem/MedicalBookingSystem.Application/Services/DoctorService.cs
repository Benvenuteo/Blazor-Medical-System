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
            {
                doctors = doctors
                    .Where(d => d.DoctorSpecializations.Any(s => s.SpecializationId == dto.SpecializationId.Value));
            }

            if (!string.IsNullOrWhiteSpace(dto.Region))
            {
                doctors = doctors
                    .Where(d => string.Equals(d.Region, dto.Region, StringComparison.OrdinalIgnoreCase));
            }

            if (dto.Date.HasValue)
            {
                var availableDoctors = await _uow.Doctors.GetAvailableDoctorsAsync(dto.Date.Value);
                var availableIds = availableDoctors.Select(d => d.Id).ToHashSet();
                doctors = doctors.Where(d => availableIds.Contains(d.Id));
            }

            // Sortowanie
            if (!string.IsNullOrWhiteSpace(dto.SortBy))
            {
                doctors = dto.SortBy?.ToLower() switch
                {
                    "rating" => doctors.OrderByDescending(d => d.AverageRating),
                    "availability" => dto.Date.HasValue
                        ? doctors.OrderBy(d => d.Schedules.Where(s => s.IsAvailable && s.StartTime >= _clock.Now).Select(s => s.StartTime).DefaultIfEmpty(DateTime.MaxValue).Min())
                        : doctors,
                    _ => doctors
                };
            }

            IEnumerable<DoctorDto> mapped = doctors.Select(item => item.ToDto()).ToList();
            return Result<IEnumerable<DoctorDto>>.Success(mapped);
        }

        public async Task<Result<IEnumerable<DoctorDto>>> GetAllAsync()
        {
            var doctors = await _uow.Doctors.GetAllAsync();
            IEnumerable<DoctorDto> dto = doctors.Select(item => item.ToDto()).ToList();
            return Result<IEnumerable<DoctorDto>>.Success(dto);
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
            await _uow.SaveChangesAsync();

            return Result.Success();
        }

    }
}
