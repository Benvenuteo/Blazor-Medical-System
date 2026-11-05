using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers;

/// <summary>Patient's own waitlist entries: notification about an earlier free slot with a chosen doctor.</summary>
[ApiController, Route("api/waitlist"), Authorize(Roles = Roles.Patient)]
public sealed class WaitlistController(WaitlistService waitlist) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Mine()
    {
        var result = await waitlist.MineAsync();
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> Join(JoinWaitlistDto dto)
    {
        var result = await waitlist.JoinAsync(dto);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.FailureResponse(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Leave(int id)
    {
        var result = await waitlist.LeaveAsync(id);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }
}
