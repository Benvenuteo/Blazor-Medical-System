using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Security;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Authorization;
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
        private readonly WorklistService _lists;
        private readonly IReviewService _reviewService;

        public ReviewsController(IReviewService reviewService, UserAccess access, WorklistService lists)
        {
            _access = access;
            _lists = lists;
            _reviewService = reviewService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _reviewService.AddReviewAsync(dto, patientId);

            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetReview), new { id = result.Value.Id }, result.Value);

            return this.FailureResponse(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _reviewService.DeleteReviewAsync(id, patientId);

            if (result.IsSuccess)
                return NoContent();

            return this.FailureResponse(result);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetReview(int id)
        {
            var result = await _reviewService.GetReviewAsync(id);

            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }

        [AllowAnonymous]
        [HttpGet("doctor/{doctorId}")]
        public async Task<IActionResult> GetDoctorReviews(int doctorId, [FromQuery] PageQueryDto query)
        {
            var result = await _lists.ReviewsAsync(doctorId, query);

            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }
    }
}
