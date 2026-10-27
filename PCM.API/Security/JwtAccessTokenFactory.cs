using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PCM.DomainEntities.Enums;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.Security;

namespace PCM.API.Security;

/// <summary>
/// Issues HS256-signed JWT access tokens with short claim names
/// (sub, email, name, role, seller_status). Lifetime: Jwt:AccessTokenMinutes.
/// </summary>
public sealed class JwtAccessTokenFactory(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenFactory
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Create(ApplicationUser user, IReadOnlyCollection<string> roles, SellerStatus? sellerStatus)
    {
        var jwt = options.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaimTypes.Subject, user.Id.ToString()),
            new(AppClaimTypes.Email, user.Email ?? string.Empty),
            new(AppClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(roles.Select(role => new Claim(AppClaimTypes.Role, role)));
        if (sellerStatus is not null)
        {
            claims.Add(new Claim(AppClaimTypes.SellerStatus, sellerStatus.Value.ToString()));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = new SigningCredentials(CreateSigningKey(jwt.SigningKey), SecurityAlgorithms.HmacSha256)
        };

        return new AccessToken(Handler.CreateToken(descriptor), expires);
    }

    /// <summary>Shared with the JwtBearer validation setup so both sides use the same key bytes.</summary>
    public static SymmetricSecurityKey CreateSigningKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));
}
