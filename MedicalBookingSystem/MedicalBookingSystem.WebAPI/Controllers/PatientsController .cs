using FluentValidation;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers
{
    [Route("api/patients")]
    [ApiController]
    public class PatientsController : ControllerBase
    {
        private readonly IPatientService _patientService;
        private readonly IValidator<UpdatePatientDto> _updateValidator;

        public PatientsController(
            IPatientService patientService,
            IValidator<UpdatePatientDto> updateValidator)
        {
            _patientService = patientService;
            _updateValidator = updateValidator;
        }
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile(int patientId)
        {
            var result = await _patientService.GetPatientProfileAsync(patientId);
            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(result.Error);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdatePatientDto dto, int patientId)
        {
            var validationResult = await _updateValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

            var result = await _patientService.UpdatePatientAsync(patientId, dto);
            if (result.IsSuccess)
                return Ok(result);

            return NotFound(result.Error);
        }
    }
}
