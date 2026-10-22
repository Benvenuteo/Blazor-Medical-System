using MedicalBookingSystem.Application.Security;
using MedicalBookingSystem.Domain.Exceptions;

namespace MedicalBookingSystem.WebAPI.Middleware;

public sealed class ExceptionMiddleware(ILogger<ExceptionMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (AccessDeniedException exception)
        {
            await Results.Problem(statusCode: exception.Authenticated ? 403 : 401,
                title: exception.Authenticated ? "Brak dostępu." : "Wymagane logowanie.").ExecuteAsync(context);
        }
        catch (NotFoundException exception)
        { await Results.Problem(statusCode: 404, detail: exception.Message).ExecuteAsync(context); }
        catch (BadRequestException exception)
        { await Results.Problem(statusCode: 400, detail: exception.Message).ExecuteAsync(context); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled request error {TraceId}", context.TraceIdentifier);
            await Results.Problem(statusCode: 500, title: "Wystąpił błąd serwera.").ExecuteAsync(context);
        }
    }
}
