using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers;

[ApiController]
[Route("api/doctor/appointments")]
[Authorize(Roles = Roles.Doctor)]
public sealed class DoctorAppointmentsController(IAppointmentService service, UserAccess access, WorklistService lists) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetScheduled([FromQuery] AppointmentListQueryDto query)
    {
        var result = await lists.DoctorAppointmentsAsync(await access.DoctorIdAsync(), false, query);
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }
    [HttpGet("completed")]
    public async Task<IActionResult> GetCompleted([FromQuery] AppointmentListQueryDto query)
    {
        var result = await lists.DoctorAppointmentsAsync(await access.DoctorIdAsync(), true, query);
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await service.CompleteAppointmentAsync(id);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }
}
