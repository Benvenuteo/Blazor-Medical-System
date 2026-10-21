using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers;

[ApiController]
[Route("api/doctor/appointments")]
[Authorize(Roles = Roles.Doctor)]
public sealed class DoctorAppointmentsController(IAppointmentService service, UserAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetScheduled() => Ok(await service.GetScheduledAppointmentsByDoctorIdAsync(await access.DoctorIdAsync()));

    [HttpGet("completed")]
    public async Task<IActionResult> GetCompleted() => Ok(await service.GetCompletedAppointmentsByDoctorIdAsync(await access.DoctorIdAsync()));

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await service.CompleteAppointmentAsync(id);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }
}
