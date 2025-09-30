using FluentValidation;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto.DtoHelp;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers
{
    [Route("api/doctors")]
    [ApiController]
    public class DoctorsController : ControllerBase
    {
        private readonly IDoctorService _doctorService;
        private readonly IReviewService _reviewService;
        private readonly IValidator<SearchDoctorsDto> _searchValidator;

        public DoctorsController(IReviewService reviewService,
            IDoctorService doctorService,
            IValidator<SearchDoctorsDto> searchValidator)
        {
            _reviewService = reviewService;
            _doctorService = doctorService;
            _searchValidator = searchValidator;
        }

        [HttpGet]
        public async Task<IActionResult> SearchDoctors([FromQuery] SearchDoctorsDto dto)
        {
            var validationResult = await _searchValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

            var result = await _doctorService.SearchDoctorsAsync(dto);
            return Ok(result.Value);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDoctorDetails(int id)
        {
            var result = await _doctorService.GetDoctorDetailsAsync(id);
            if (result.IsSuccess)
                return Ok(result);

            return NotFound(result.Error);
        }

        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetDoctorReviews(int id)
        {
            var result = await _reviewService.GetDoctorReviewsAsync(id);
            if (result.IsSuccess)
                return Ok(result);

            return NotFound(result.Error);
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _doctorService.GetAllAsync();

            if (result.IsSuccess)
                return Ok(result.Value);

            return BadRequest(result.Error);
        }

        [HttpGet("specializations")]
        public async Task<IActionResult> GetSpecializations()
        {
            var result = await _doctorService.GetSpecializationsAsync();

            if (result.IsSuccess)
                return Ok(result.Value);

            return BadRequest(result.Error);
        }

        [HttpGet("{doctorId}/schedule")]
        public async Task<IActionResult> GetSchedule(int doctorId)
        {
            var result = await _doctorService.GetDoctorScheduleAsync(doctorId);
            if (!result.IsSuccess)
                return NotFound(result.Error);

            return Ok(result.Value);
        }
    }
}
