using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCM.Infrastructure.Exceptions;

namespace PCM.API.Middleware;

/// <summary>
/// Converts exceptions into RFC 9457 ProblemDetails responses.
/// Known business exceptions map to their status code; anything else is logged
/// as an error and returned as a generic 500 without internal details.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict",
                "The resource was changed by another request. Reload and try again."),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", null)
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request failed with {StatusCode}: {Message}", status, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });
    }
}
