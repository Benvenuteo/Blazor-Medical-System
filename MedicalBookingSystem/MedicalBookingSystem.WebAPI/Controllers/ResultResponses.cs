using MedicalBookingSystem.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace MedicalBookingSystem.WebAPI.Controllers;

public static class ResultResponses
{
    public static ObjectResult FailureResponse(this ControllerBase controller, Result result) => controller.Problem(
        detail: result.Error, statusCode: result.ErrorType switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        });
}
