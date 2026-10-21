using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers
{
    [Route("api/reviews")]
    [ApiController]
    [Authorize(Roles = Roles.Patient)]
    public class ReviewsController : ControllerBase
    {
        private readonly UserAccess _access;
        private readonly IReviewService _reviewService;
        private readonly IValidator<CreateReviewDto> _createValidator;

        public ReviewsController(
            IReviewService reviewService,
            IValidator<CreateReviewDto> createValidator, UserAccess access)
        {
            _access = access;
            _reviewService = reviewService;
            _createValidator = createValidator;
        }

        [HttpPost]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            var patientId = await _access.PatientIdAsync();
            var validation = await _createValidator.ValidateAsync(dto);
            if (!validation.IsValid)
                return BadRequest(validation.Errors.Select(error => error.ErrorMessage));
            var result = await _reviewService.AddReviewAsync(dto, patientId);

            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetReview), new { id = result.Value.Id }, result.Value);

            return BadRequest(result.Error);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _reviewService.DeleteReviewAsync(id, patientId);

            if (result.IsSuccess)
                return NoContent();

            return BadRequest(result.Error);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReview(int id)
        {
            var result = await _reviewService.GetReviewAsync(id);

            if (result.IsSuccess)
                return Ok(result);

            return NotFound(result.Error);
        }

        [AllowAnonymous]
        [HttpGet("doctor/{doctorId}")]
        public async Task<IActionResult> GetDoctorReviews(int doctorId)
        {
            var result = await _reviewService.GetDoctorReviewsAsync(doctorId);

            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(result.Error);
        }
    }
}
