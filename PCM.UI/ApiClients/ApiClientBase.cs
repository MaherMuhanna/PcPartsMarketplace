using System.Net.Http.Json;

namespace PCM.UI.ApiClients;

/// <summary>
/// Shared plumbing for the typed API clients: JSON in and out, and every
/// non-success response turned into an <see cref="ApiException"/> carrying the
/// API's ProblemDetails. The bearer token is added by <see cref="Security.ApiTokenHandler"/>.
/// </summary>
public abstract class ApiClientBase(HttpClient httpClient)
{
    protected HttpClient Http { get; } = httpClient;

    protected async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(url, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    protected async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, url, body);
        using var response = await Http.SendAsync(request, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    protected async Task SendAsync(HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, url, body);
        using var response = await Http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, object? body) =>
        new(method, url) { Content = body is null ? null : JsonContent.Create(body, body.GetType()) };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new ApiException(response.StatusCode, new ApiProblem { Detail = "The API returned an empty response." });
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiProblem? problem = null;
        if (response.Content.Headers.ContentType?.MediaType is "application/problem+json" or "application/json")
        {
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ApiProblem>(cancellationToken);
            }
            catch (System.Text.Json.JsonException)
            {
                // Not a ProblemDetails body; fall back to the status code only.
            }
        }

        throw new ApiException(response.StatusCode, problem);
    }
}
