using AutoMapper;
using MedicalBookingSystem.Application.Validators;
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
        private readonly IMapper _mapper;

        public DoctorService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }

        public async Task<Result<DoctorDetailsDto>> GetDoctorDetailsAsync(int id)
        {
            var doctor = await _uow.Doctors.GetByIdAsync(id);
            if (doctor == null)
                return Result<DoctorDetailsDto>.Failure<DoctorDetailsDto>("Lekarz nie został znaleziony.");

            var dto = _mapper.Map<DoctorDetailsDto>(doctor);
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

            var dto = _mapper.Map<IEnumerable<DoctorScheduleDto>>(schedules);
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
            if (!string.IsNullOrWhiteSpace(dto.Region))
            {
                doctors = dto.SortBy?.ToLower() switch
                {
                    "rating" => doctors.OrderByDescending(d => d.AverageRating),
                    "availability" => dto.Date.HasValue
                        ? doctors.OrderBy(d => d.Schedules.Any(s => s.StartTime == dto.Date.Value && s.IsAvailable))
                        : doctors,
                    _ => doctors
                };
            }

            var mapped = _mapper.Map<IEnumerable<DoctorDto>>(doctors);
            return Result<IEnumerable<DoctorDto>>.Success(mapped);
        }

        public async Task<Result<IEnumerable<DoctorDto>>> GetAllAsync()
        {
            var doctors = await _uow.Doctors.GetAllAsync();
            var dto = _mapper.Map<IEnumerable<DoctorDto>>(doctors);
            return Result<IEnumerable<DoctorDto>>.Success(dto);
        }

        public async Task<Result<IEnumerable<SpecializationDto>>> GetSpecializationsAsync()
        {
            var specializations = await _uow.Specializations.GetAllAsync();
            var dto = _mapper.Map<IEnumerable<SpecializationDto>>(specializations);
            return Result<IEnumerable<SpecializationDto>>.Success(dto);
        }

        public async Task<bool> SetDoctorImageUrlAsync(int doctorId, string imageUrl)
        {
            var doctor = await _uow.Doctors.GetByIdAsync(doctorId);
            if (doctor == null) return false;

            doctor.ImageUrl = imageUrl;
            await _uow.SaveChangesAsync();
            return true;
        }

        public async Task<Result> CreateScheduleAsync(CreateScheduleDto dto)
        {
            var validator = new CreateScheduleDtoValidator(_uow);
            var validationResult = validator.ValidateAsync(dto);

            if (!validationResult.IsCompletedSuccessfully)
                return Result.Failure(string.Join("; ", validationResult?.Exception?.Message));

            var schedule = new DoctorSchedule
            {
                DoctorId = dto.DoctorId,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                IsAvailable = true
            };

            _uow.DoctorSchedules.AddAsync(schedule);
            await _uow.SaveChangesAsync();

            return Result.Success();
        }

    }
}
