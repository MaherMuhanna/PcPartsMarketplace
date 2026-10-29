namespace PCM.Infrastructure.Exceptions;

// Business exceptions thrown by services. The API's GlobalExceptionHandler maps
// each one to an HTTP status code, so services never deal with HTTP.

/// <summary>Requested resource does not exist, or does not belong to the caller (404).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Request conflicts with the current state, e.g. stock or price changed (409).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Credentials or token are invalid, expired, revoked, or the account is locked (401).</summary>
public sealed class UnauthorizedException(string message) : Exception(message);

/// <summary>
/// Input is well-formed but breaks a rule that needs the database to check, e.g. an option id
/// that does not belong to the attribute (400). Same response shape as FluentValidation errors:
/// field name (camelCase, as the client sent it) to messages.
/// </summary>
public sealed class RequestValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    /// <summary>Single-field shortcut.</summary>
    public RequestValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }
}
