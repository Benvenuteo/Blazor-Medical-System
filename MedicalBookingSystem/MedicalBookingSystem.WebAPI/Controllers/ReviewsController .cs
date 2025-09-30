using FluentValidation;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto.ReviewsDto;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers
{
    [Route("api/reviews")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly IValidator<CreateReviewDto> _createValidator;

        public ReviewsController(
            IReviewService reviewService,
            IValidator<CreateReviewDto> createValidator)
        {
            _reviewService = reviewService;
            _createValidator = createValidator;
        }

        [HttpPost]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto, int patientId)
        {
            var result = await _reviewService.AddReviewAsync(dto, patientId);

            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetReview), new { id = result.Value.Id }, result.Value);

            return BadRequest(result.Error);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReview(int id, int patientId)
        {
            var result = await _reviewService.DeleteReviewAsync(id, patientId);

            if (result.IsSuccess)
                return NoContent();

            return BadRequest(result.Error);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReview(int id)
        {
            var result = await _reviewService.GetReviewAsync(id);

            if (result.IsSuccess)
                return Ok(result);

            return NotFound(result.Error);
        }

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
