using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TransfersApp.Domain.Exceptions;

namespace TransfersApp;

public class ExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            AccountNotFoundException => (StatusCodes.Status404NotFound, "Account Not Found"),
            InsufficientFundsException => (StatusCodes.Status422UnprocessableEntity, "Insufficient Funds"),
            _ => (0, null)
        };

        if (statusCode == 0) return false;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        };
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
