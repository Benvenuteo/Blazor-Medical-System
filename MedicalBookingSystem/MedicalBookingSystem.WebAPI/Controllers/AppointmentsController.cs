using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using FluentValidation;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto.AppointmentsDto;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers
{
    [Route("api/appointments")]
    [ApiController]
    [Authorize(Roles = Roles.Patient)]
    public class AppointmentsController : ControllerBase
    {
        private readonly UserAccess _access;
        private readonly IAppointmentService _appointmentService;
        private readonly IValidator<CreateAppointmentDto> _createValidator;
        private readonly IValidator<UpdateAppointmentDto> _updateValidator;

        public AppointmentsController(
            IAppointmentService appointmentService,
            IValidator<CreateAppointmentDto> createValidator,
            IValidator<UpdateAppointmentDto> updateValidator, UserAccess access)
        {
            _access = access;
            _appointmentService = appointmentService;
            _createValidator = createValidator;
            _updateValidator = updateValidator;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentDto dto)
        {
            var validationResult = await _createValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

            var patientId = await _access.PatientIdAsync();
            var result = await _appointmentService.CreateAppointmentAsync(dto, patientId);

            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetAppointment), new { id = result.Value.Id }, result.Value);

            return BadRequest(result.Error);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAppointment(int id, [FromBody] UpdateAppointmentDto dto)
        {
            var validationResult = await _updateValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
                return BadRequest(validationResult.Errors.Select(e => e.ErrorMessage));

            var result = await _appointmentService.UpdateAppointmentAsync(id, dto);

            if (result.IsSuccess)
                return Ok(result);

            return NotFound(result.Error);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _appointmentService.CancelAppointmentAsync(id, patientId);

            if (result.IsSuccess)
                return NoContent();

            return NotFound(result.Error);
        }

        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcomingAppointments()
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _appointmentService.GetUpcomingAppointmentsAsync(patientId);

            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(result.Error);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetAppointmentHistory()
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _appointmentService.GetPastAppointmentsAsync(patientId);

            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(result.Error);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointment(int id)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _appointmentService.GetAppointmentDetailsAsync(id, patientId);

            if (result.IsSuccess)
                return Ok(result.Value);

            return NotFound(result.Error);
        }
    }
}
