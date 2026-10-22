using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Authorization;
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
        private readonly WorklistService _lists;
        private readonly IReviewService _reviewService;
        private readonly IValidator<SearchDoctorsDto> _searchValidator;

        public DoctorsController(IReviewService reviewService,
            IDoctorService doctorService,
            IValidator<SearchDoctorsDto> searchValidator, WorklistService lists)
        {
            _lists = lists;
            _reviewService = reviewService;
            _doctorService = doctorService;
            _searchValidator = searchValidator;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SearchDoctors([FromQuery] SearchDoctorsDto dto)
        {
            var validationResult = await _searchValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return Problem(statusCode: 400, detail: string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));

            var result = await _doctorService.SearchDoctorsAsync(dto);
            return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetDoctorDetails(int id)
        {
            var result = await _doctorService.GetDoctorDetailsAsync(id);
            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }

        [AllowAnonymous]
        [HttpGet("{id}/reviews")]
        public async Task<IActionResult> GetDoctorReviews(int id, [FromQuery] PageQueryDto query)
        {
            var result = await _lists.ReviewsAsync(id, query);
            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }

        [AllowAnonymous]
        [HttpGet("specializations")]
        public async Task<IActionResult> GetSpecializations()
        {
            var result = await _doctorService.GetSpecializationsAsync();

            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }

        [AllowAnonymous]
        [HttpGet("{doctorId:int}/slots")]
        public async Task<IActionResult> GetAvailableSlots(int doctorId, DateTime? date, int? excludedAppointmentId)
        {
            var result = await _doctorService.GetAvailableSlotsAsync(doctorId, date, excludedAppointmentId);
            return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
        }

        [AllowAnonymous]
        [HttpGet("{doctorId}/schedule")]
        public async Task<IActionResult> GetSchedule(int doctorId)
        {
            var result = await _doctorService.GetDoctorScheduleAsync(doctorId);
            if (!result.IsSuccess)
                return this.FailureResponse(result);

            return Ok(result.Value);
        }
    }
}
