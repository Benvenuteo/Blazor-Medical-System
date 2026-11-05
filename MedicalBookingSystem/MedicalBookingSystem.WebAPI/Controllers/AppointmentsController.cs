using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
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
        private readonly WorklistService _lists;
        private readonly UserAccess _access;
        private readonly IAppointmentService _appointmentService;

        public AppointmentsController(IAppointmentService appointmentService, UserAccess access, WorklistService lists)
        {
            _lists = lists;
            _access = access;
            _appointmentService = appointmentService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentDto dto)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _appointmentService.CreateAppointmentAsync(dto, patientId);

            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetAppointment), new { id = result.Value.Id }, result.Value);

            return this.FailureResponse(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAppointment(int id, [FromBody] UpdateAppointmentDto dto)
        {
            var result = await _appointmentService.UpdateAppointmentAsync(id, dto);

            if (result.IsSuccess)
                return NoContent();

            return this.FailureResponse(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> CancelAppointment(int id)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _appointmentService.CancelAppointmentAsync(id, patientId);

            if (result.IsSuccess)
                return NoContent();

            return this.FailureResponse(result);
        }

        [HttpGet("upcoming")]
        public async Task<IActionResult> GetUpcomingAppointments([FromQuery] AppointmentListQueryDto query)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _lists.PatientAppointmentsAsync(patientId, false, query);

            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetAppointmentHistory([FromQuery] AppointmentListQueryDto query)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _lists.PatientAppointmentsAsync(patientId, true, query);

            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }

        /// <summary>iCalendar file of a scheduled visit (add to Google, Outlook or Apple Calendar).</summary>
        [HttpGet("{id:int}/calendar")]
        public async Task<IActionResult> GetCalendarFile(int id, [FromServices] MedicalBookingSystem.Application.Notifications.VisitNotificationService notifications)
        {
            var result = await notifications.CalendarFileAsync(id);
            return result.IsSuccess
                ? File(System.Text.Encoding.UTF8.GetBytes(result.Value.Content), result.Value.ContentType, result.Value.FileName)
                : this.FailureResponse(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAppointment(int id)
        {
            var patientId = await _access.PatientIdAsync();
            var result = await _appointmentService.GetAppointmentDetailsAsync(id, patientId);

            if (result.IsSuccess)
                return Ok(result.Value);

            return this.FailureResponse(result);
        }
    }
}
