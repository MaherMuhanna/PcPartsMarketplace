using System.Net;

namespace PCM.UI.ApiClients;

/// <summary>RFC 9457 ProblemDetails body returned by PCM.API on every error.</summary>
public sealed class ApiProblem
{
    public string? Title { get; init; }

    public string? Detail { get; init; }

    public int? Status { get; init; }

    /// <summary>Validation errors keyed by camelCase field name (400 only).</summary>
    public Dictionary<string, string[]>? Errors { get; init; }
}

/// <summary>A non-success response from the API, with its ProblemDetails when present.</summary>
public sealed class ApiException(HttpStatusCode statusCode, ApiProblem? problem)
    : Exception(problem?.Detail ?? problem?.Title ?? $"The API returned {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public ApiProblem? Problem { get; } = problem;

    /// <summary>Best message to show a user.</summary>
    public string UserMessage => Problem?.Detail ?? Problem?.Title ?? "Something went wrong. Please try again.";
}
