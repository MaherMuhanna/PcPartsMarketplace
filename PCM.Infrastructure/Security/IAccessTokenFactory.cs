using PCM.DomainEntities.Enums;
using PCM.DomainEntities.Identity;

namespace PCM.Infrastructure.Security;

/// <summary>A signed access token and its UTC expiry.</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>
/// Creates signed JWT access tokens. Implemented in PCM.API (where the JWT
/// libraries live); services depend only on this interface, which keeps them unit-testable.
/// </summary>
public interface IAccessTokenFactory
{
    AccessToken Create(ApplicationUser user, IReadOnlyCollection<string> roles, SellerStatus? sellerStatus);
}
