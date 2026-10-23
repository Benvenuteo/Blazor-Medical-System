using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto.Accounts;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers;

[ApiController, Route("api/staff/accounts"), Authorize(Roles = Roles.Admin)]
public sealed class StaffAccountsController(StaffAccountService accounts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AccountSearchDto query)
    {
        var result = await accounts.SearchAsync(query);
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }

    [HttpGet("unassigned-doctors")]
    public async Task<IActionResult> UnassignedDoctors([FromQuery] AccountSearchDto query)
    {
        var result = await accounts.UnassignedDoctorsAsync(query);
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var result = await accounts.GetAsync(id);
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffAccountDto dto)
    {
        var result = await accounts.CreateAsync(dto);
        return result.IsSuccess ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value) : this.FailureResponse(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateStaffAccountDto dto)
    {
        var result = await accounts.UpdateAsync(id, dto);
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetStaffPasswordDto dto)
    {
        var result = await accounts.ResetPasswordAsync(id, dto);
        return result.IsSuccess ? NoContent() : this.FailureResponse(result);
    }
}
