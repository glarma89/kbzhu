using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NutritionTracker.Application.Common;

namespace NutritionTracker.Api.Errors;

internal sealed class ApplicationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ApplicationValidationException validationException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = validationException.Message,
                Instance = httpContext.Request.Path
            },
            EntityNotFoundException notFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found",
                Detail = notFoundException.Message,
                Instance = httpContext.Request.Path
            },
            _ => null
        };

        if (problemDetails is null)
        {
            return false;
        }

        if (exception is ApplicationValidationException { ParameterName: not null } validation)
        {
            problemDetails.Extensions["parameter"] = validation.ParameterName;
        }

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
        await Results.Problem(problemDetails).ExecuteAsync(httpContext);
        return true;
    }
}
