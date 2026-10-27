using System.ComponentModel.DataAnnotations;

namespace PCM.Infrastructure.Security;

/// <summary>
/// "Jwt" configuration section. Issuer, audience and lifetimes live in appsettings.json;
/// the signing key comes only from user-secrets or environment variables (Jwt__SigningKey).
/// Validated at startup.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 key; at least 32 characters (256 bits).</summary>
    [Required]
    [MinLength(32, ErrorMessage = "Jwt:SigningKey must be at least 32 characters.")]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Access token lifetime (agreed: 60 minutes).</summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>Refresh token lifetime (agreed: 30 days).</summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}
