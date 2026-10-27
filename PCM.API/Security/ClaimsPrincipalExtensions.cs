using System.Globalization;
using System.Security.Claims;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Security;

namespace PCM.API.Security;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The current user's id, read from the JWT "sub" claim. This is the ONLY source
    /// of the owner id: clients never send UserId / CustomerId / SellerId (IDOR rule).
    /// </summary>
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(AppClaimTypes.Subject);
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new UnauthorizedException("The access token has no valid subject.");
    }
}
