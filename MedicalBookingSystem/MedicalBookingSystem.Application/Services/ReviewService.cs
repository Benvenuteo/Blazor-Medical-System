using FluentValidation;
using MedicalBookingSystem.Domain.Exceptions;
using MedicalBookingSystem.Domain.Scheduling;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Time;
using MedicalBookingSystem.Application.Mappings;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWorkFactory _units;
        private readonly UserAccess _access;
        private readonly ClinicClock _clock;
        private readonly IValidator<CreateReviewDto> _validator;


        public ReviewService(IUnitOfWorkFactory units, ClinicClock clock, UserAccess access, IValidator<CreateReviewDto> validator)
        {
            _units = units;
            _access = access;
            _clock = clock;
            _validator = validator;

        }
        public async Task<Result<ReviewDto>> AddReviewAsync(CreateReviewDto dto, int patientId)
        {
            using var _uow = _units.Create();
            await _access.RequirePatientAsync(patientId);
            var validation = await _validator.ValidateAsync(dto);
            if (!validation.IsValid) return Result.Failure<ReviewDto>(string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
            if (await _uow.Doctors.GetByIdAsync(dto.DoctorId) is null)
                return Result.Failure<ReviewDto>("Lekarz nie istnieje.", ErrorType.NotFound);
            var latestCompletedStart = _clock.Now.Subtract(AppointmentSlots.Duration);
            if (!await _uow.Appointments.AnyAsync(a => a.PatientId == patientId && a.DoctorId == dto.DoctorId &&
                a.Status == AppointmentStatus.Completed && a.Date <= latestCompletedStart))
                return Result.Failure<ReviewDto>("Opinię można dodać po odbytej wizycie u tego lekarza.", ErrorType.Conflict);
            if (await _uow.Reviews.HasPatientReviewedDoctorAsync(patientId, dto.DoctorId))
                return Result.Failure<ReviewDto>("Możesz dodać tylko jedną opinię na lekarza.", ErrorType.Conflict);
            var review = new Review { DoctorId = dto.DoctorId, PatientId = patientId, Rating = dto.Rating,
                Comment = dto.Comment, CreatedDate = _clock.UtcNow };
            await _uow.Reviews.AddAsync(review);
            try { await _uow.SaveChangesAsync(); }
            catch (PersistenceConflictException)
            {
                return Result.Failure<ReviewDto>("Opinia została już dodana w innym żądaniu.", ErrorType.Conflict);
            }
            return Result.Success(review.ToDto());
        }

        public async Task<Result> DeleteReviewAsync(int reviewId, int patientId)
        {
            using var _uow = _units.Create();
            await _access.RequirePatientAsync(patientId);
            var review = await _uow.Reviews.GetByIdAsync(reviewId);
            if (review == null)
                return Result.Failure("Opinia nie istnieje", ErrorType.NotFound);

            if (review.PatientId != patientId)
                throw new AccessDeniedException(true);

            _uow.Reviews.Remove(review);
            await _uow.SaveChangesAsync();

            return Result.Success();
        }



        public async Task<Result<ReviewDto>> GetReviewAsync(int reviewId)
        {
            using var _uow = _units.Create();
            var review = await _uow.Reviews.GetByIdAsync(reviewId);
            if (review == null)
                return Result<ReviewDto>.Failure<ReviewDto>("Nie znaleziono opinii.", ErrorType.NotFound);

            var dto = review.ToDto();
            return Result<ReviewDto>.Success(dto);
        }
    }
}
