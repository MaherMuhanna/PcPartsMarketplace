using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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
        // Rule violations found by a service: same 400 shape as FluentValidation errors.
        if (exception is RequestValidationException validation)
        {
            logger.LogInformation("Request failed with 400: {Fields}", string.Join(", ", validation.Errors.Keys));
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new HttpValidationProblemDetails(
                    validation.Errors.ToDictionary(e => e.Key, e => e.Value))
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred."
                }
            });
        }

        var (status, title, detail) = exception switch
        {
            UnauthorizedException e => (StatusCodes.Status401Unauthorized, "Unauthorized", e.Message),
            NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict",
                "The resource was changed by another request. Reload and try again."),
            // A unique index caught a duplicate that slipped past the service pre-check (race).
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict, "Conflict",
                "A record with the same unique value already exists."),
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
