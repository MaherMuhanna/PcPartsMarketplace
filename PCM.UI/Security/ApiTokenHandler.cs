using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PCM.Infrastructure.DTO.Auth;

namespace PCM.UI.Security;

/// <summary>
/// Outgoing handler for every typed API client:
/// <list type="number">
///   <item>Adds "Authorization: Bearer {access token}" from the auth cookie.</item>
///   <item>Refreshes the token pair first when the access token expires within a minute.</item>
///   <item>On a 401 (e.g. the API restarted with a new key) refreshes once and retries.</item>
///   <item>If the refresh fails, signs the cookie out; the 401 then sends the user to the login page.</item>
/// </list>
/// Refreshes are serialized per request, so parallel API calls never present the same
/// refresh token twice (that would trigger the API's reuse detection).
/// </summary>
public sealed class ApiTokenHandler(
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider,
    ILogger<ApiTokenHandler> logger) : DelegatingHandler
{
    /// <summary>Named client without this handler, used only for POST /auth/refresh.</summary>
    public const string RefreshClientName = "pcm-api-refresh";

    private const string RefreshLockKey = "pcm.refresh-lock";
    private static readonly TimeSpan RefreshAhead = TimeSpan.FromMinutes(1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var tokens = httpContext is null ? null : await AuthSession.GetTokensAsync(httpContext);
        if (httpContext is null || tokens is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var refreshed = false;
        if (tokens.AccessTokenExpiresAt - RefreshAhead <= timeProvider.GetUtcNow().UtcDateTime)
        {
            tokens = await RefreshAsync(httpContext, tokens, cancellationToken);
            refreshed = true;
            if (tokens is null)
            {
                return await base.SendAsync(request, cancellationToken);
            }
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized || refreshed)
        {
            return response;
        }

        var newTokens = await RefreshAsync(httpContext, tokens, cancellationToken);
        if (newTokens is null)
        {
            return response;
        }

        response.Dispose();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newTokens.AccessToken);
        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<ApiTokens?> RefreshAsync(HttpContext httpContext, ApiTokens used, CancellationToken cancellationToken)
    {
        var gate = GetRefreshLock(httpContext);
        await gate.WaitAsync(cancellationToken);
        try
        {
            // Another call in this request may have refreshed while we waited.
            var latest = await AuthSession.GetTokensAsync(httpContext);
            if (latest is null || latest.RefreshToken != used.RefreshToken)
            {
                return latest;
            }

            var client = httpClientFactory.CreateClient(RefreshClientName);
            using var response = await client.PostAsJsonAsync(
                "api/v1/auth/refresh", new RefreshTokenRequest { RefreshToken = used.RefreshToken }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Token refresh failed with {StatusCode}; signing the user out", (int)response.StatusCode);
                await AuthSession.SignOutAsync(httpContext);
                return null;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
            if (auth is null)
            {
                await AuthSession.SignOutAsync(httpContext);
                return null;
            }

            // Re-issues the cookie with the new tokens and fresh claims (e.g. a seller just approved).
            await AuthSession.SignInAsync(httpContext, auth);
            return new ApiTokens(auth.AccessToken, auth.AccessTokenExpiresAt, auth.RefreshToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static SemaphoreSlim GetRefreshLock(HttpContext httpContext)
    {
        if (httpContext.Items[RefreshLockKey] is not SemaphoreSlim gate)
        {
            gate = new SemaphoreSlim(1, 1);
            httpContext.Items[RefreshLockKey] = gate;
        }

        return gate;
    }
}
