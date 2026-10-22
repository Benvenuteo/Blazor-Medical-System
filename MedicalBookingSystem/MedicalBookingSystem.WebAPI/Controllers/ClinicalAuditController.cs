using MedicalBookingSystem.Application.Services;
using MedicalBookingSystem.SharedKernel.Dto;
using MedicalBookingSystem.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers;

[ApiController, Route("api/admin/clinical-audit"), Authorize(Roles = Roles.Admin)]
public sealed class ClinicalAuditController(WorklistService lists) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] PageQueryDto query)
    {
        var result = await lists.AuditAsync(query);
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }
}
