using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.Security;

namespace PCM.UI.Security;

/// <summary>The API tokens of the signed-in user, kept inside the encrypted auth cookie.</summary>
public sealed record ApiTokens(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken);

/// <summary>
/// Bridges the API's JWTs and the UI's cookie session.
/// The browser only ever holds the encrypted, HttpOnly cookie; the access and refresh
/// tokens stay inside it (AuthenticationProperties tokens), so page scripts cannot read them.
/// </summary>
public static class AuthSession
{
    public const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;

    private const string AccessTokenName = "access_token";
    private const string AccessTokenExpiresName = "access_token_expires_at";
    private const string RefreshTokenName = "refresh_token";

    /// <summary>HttpContext.Items key: tokens issued during this request (newer than the incoming cookie).</summary>
    private const string CurrentTokensKey = "pcm.api-tokens";

    /// <summary>Signs the user in (or refreshes the cookie) from an API auth response.</summary>
    public static async Task SignInAsync(HttpContext httpContext, AuthResponse auth, bool? isPersistent = null)
    {
        var identity = new ClaimsIdentity(
            BuildClaims(auth.User),
            Scheme,
            nameType: AppClaimTypes.Name,
            roleType: AppClaimTypes.Role);

        var properties = new AuthenticationProperties
        {
            // On a token refresh keep the "remember me" choice of the original sign-in.
            IsPersistent = isPersistent ?? (await httpContext.AuthenticateAsync(Scheme)).Properties?.IsPersistent ?? false,
            ExpiresUtc = auth.RefreshTokenExpiresAt
        };
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = AccessTokenName, Value = auth.AccessToken },
            new AuthenticationToken { Name = AccessTokenExpiresName, Value = auth.AccessTokenExpiresAt.ToString("o", CultureInfo.InvariantCulture) },
            new AuthenticationToken { Name = RefreshTokenName, Value = auth.RefreshToken }
        ]);

        await httpContext.SignInAsync(Scheme, new ClaimsPrincipal(identity), properties);

        httpContext.Items[CurrentTokensKey] = new ApiTokens(auth.AccessToken, auth.AccessTokenExpiresAt, auth.RefreshToken);
    }

    public static async Task SignOutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(Scheme);
        httpContext.Items[CurrentTokensKey] = null;
    }

    /// <summary>The tokens to use for API calls in this request, or null when signed out.</summary>
    public static async Task<ApiTokens?> GetTokensAsync(HttpContext httpContext)
    {
        // Tokens refreshed (or cleared) earlier in this request win over the incoming cookie.
        if (httpContext.Items.TryGetValue(CurrentTokensKey, out var current))
        {
            return current as ApiTokens;
        }

        var result = await httpContext.AuthenticateAsync(Scheme);
        if (!result.Succeeded || result.Properties is null)
        {
            return null;
        }

        var accessToken = result.Properties.GetTokenValue(AccessTokenName);
        var refreshToken = result.Properties.GetTokenValue(RefreshTokenName);
        var expiresAt = result.Properties.GetTokenValue(AccessTokenExpiresName);

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) ||
            !DateTime.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expires))
        {
            return null;
        }

        return new ApiTokens(accessToken, expires.ToUniversalTime(), refreshToken);
    }

    private static IEnumerable<Claim> BuildClaims(UserDto user)
    {
        yield return new Claim(AppClaimTypes.Subject, user.Id.ToString(CultureInfo.InvariantCulture));
        yield return new Claim(AppClaimTypes.Name, user.FullName);
        yield return new Claim(AppClaimTypes.Email, user.Email);

        foreach (var role in user.Roles)
        {
            yield return new Claim(AppClaimTypes.Role, role);
        }

        if (user.SellerStatus is not null)
        {
            yield return new Claim(AppClaimTypes.SellerStatus, user.SellerStatus);
        }
    }
}
