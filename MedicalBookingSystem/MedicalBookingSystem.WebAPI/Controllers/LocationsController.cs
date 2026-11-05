using MedicalBookingSystem.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MedicalBookingSystem.WebAPI.Controllers;

/// <summary>Turns an address typed by a signed-in user into map coordinates (the patient search centre).</summary>
[ApiController, Route("api/locations"), Authorize, EnableRateLimiting("geocoding")]
public sealed class LocationsController(ClinicService clinics) : ControllerBase
{
    [HttpGet("geocode")]
    public async Task<IActionResult> Geocode([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var result = await clinics.GeocodeAsync(q, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.FailureResponse(result);
    }
}
