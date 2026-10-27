namespace PCM.Infrastructure.Exceptions;

// Business exceptions thrown by services. The API's GlobalExceptionHandler maps
// each one to an HTTP status code, so services never deal with HTTP.

/// <summary>Requested resource does not exist, or does not belong to the caller (404).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Request conflicts with the current state, e.g. stock or price changed (409).</summary>
public sealed class ConflictException(string message) : Exception(message);
