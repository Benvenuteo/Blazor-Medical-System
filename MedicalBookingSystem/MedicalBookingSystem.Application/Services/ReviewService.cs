using AutoMapper;
using MedicalBookingSystem.Domain.Contracts;
using MedicalBookingSystem.Domain.Models;
using MedicalBookingSystem.SharedKernel;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;

namespace MedicalBookingSystem.Application.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;

        public ReviewService(IUnitOfWork uow, IMapper mapper)
        {
            _uow = uow;
            _mapper = mapper;
        }
        public async Task<Result<ReviewDto>> AddReviewAsync(CreateReviewDto dto, int patientId)
        {
            try
            {
                // 1. Walidacja lekarza
                var doctor = await _uow.Doctors.GetByIdAsync(dto.DoctorId);
                if (doctor == null)
                    return Result<ReviewDto>.Failure<ReviewDto>("Lekarz nie istnieje");

                // 2. Sprawdź czy pacjent miał wizytę u tego lekarza
                var hadAppointment = await _uow.Appointments
                    .AnyAsync(a => a.PatientId == patientId &&
                                  a.DoctorId == dto.DoctorId &&
                                  a.Status == AppointmentStatus.Completed);

                if (!hadAppointment)
                    return Result<ReviewDto>.Failure<ReviewDto>("Możesz dodawać opinie tylko do lekarzy, u których miałeś wizytę");

                // 3. Sprawdź czy pacjent już dodał opinię dla tego lekarza
                var existingReview = await _uow.Reviews.HasPatientReviewedDoctorAsync(patientId, dto.DoctorId);


                if (existingReview)
                    return Result<ReviewDto>.Failure<ReviewDto>("Możesz dodać tylko jedną opinię na lekarza");

                // 4. Utwórz opinię
                var review = new Review
                {
                    DoctorId = dto.DoctorId,
                    PatientId = patientId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    CreatedDate = DateTime.UtcNow
                };

                await _uow.Reviews.AddAsync(review);
                await _uow.SaveChangesAsync();

                return Result<ReviewDto>.Success(_mapper.Map<ReviewDto>(review));
            }
            catch (Exception ex)
            {
                return Result<ReviewDto>.Failure<ReviewDto>("Wystąpił błąd podczas dodawania opinii");
            }
        }



        public async Task<Result> DeleteReviewAsync(int reviewId, int patientId)
        {
            try
            {
                var review = await _uow.Reviews.GetByIdAsync(reviewId);
                if (review == null)
                    return Result.Failure("Opinia nie istnieje");

                if (review.PatientId != patientId)
                    return Result.Failure("Nie możesz usunąć cudzej opinii");

                _uow.Reviews.Remove(review);
                await _uow.SaveChangesAsync();

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure("Wystąpił błąd podczas usuwania opinii");
            }
        }

        public async Task<Result<IEnumerable<ReviewDto>>> GetDoctorReviewsAsync(int doctorId)
        {
            try
            {
                var reviews = await _uow.Reviews
                    .GetByDoctorIdAsync(doctorId);

                var reviewDtos = _mapper.Map<IEnumerable<ReviewDto>>(reviews);
                return Result<IEnumerable<ReviewDto>>.Success(reviewDtos);
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<ReviewDto>>.Failure<IEnumerable<ReviewDto>>("Wystąpił błąd podczas pobierania opinii");
            }
        }

        public async Task<Result<ReviewDto>> GetReviewAsync(int reviewId)
        {
            var review = await _uow.Reviews.GetByIdAsync(reviewId);
            if (review == null)
                return Result<ReviewDto>.Failure<ReviewDto>("Nie znaleziono opinii.");

            var dto = _mapper.Map<ReviewDto>(review);
            return Result<ReviewDto>.Success(dto);
        }
    }
}
