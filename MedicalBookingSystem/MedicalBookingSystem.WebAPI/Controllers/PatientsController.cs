using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers
{
    [Route("api/patients")]
    [ApiController]
    [Authorize(Roles = Roles.Patient)]
    public class PatientsController : ControllerBase
    {
        private readonly UserAccess _access;
        private readonly IPatientService _patientService;
        private readonly IValidator<UpdatePatientDto> _updateValidator;

        public PatientsController(
            IPatientService patientService,
            IValidator<UpdatePatientDto> updateValidator, UserAccess access)
        {
            _access = access;
            _patientService = patientService;
            _updateValidator = updateValidator;
        }
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _patientService.GetPatientProfileAsync(patientId);
            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdatePatientDto dto)
        {
            var patientId = await _access.PatientIdAsync();
            var validationResult = await _updateValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return Problem(statusCode: 400, detail: string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage)));

            var result = await _patientService.UpdatePatientAsync(patientId, dto);
            if (result.IsSuccess)
                return NoContent();

            return this.FailureResponse(result);
        }
    }
}
