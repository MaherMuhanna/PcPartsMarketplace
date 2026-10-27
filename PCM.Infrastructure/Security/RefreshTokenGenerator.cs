using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace PCM.Infrastructure.Security;

/// <summary>
/// Opaque refresh tokens: 64 random bytes, Base64Url-encoded for the client.
/// Only the SHA-256 hash (64 lowercase hex chars, column RefreshToken.TokenHash char(64))
/// is stored, so a database leak does not expose usable tokens.
/// </summary>
public static class RefreshTokenGenerator
{
    private const int TokenBytes = 64;

    /// <summary>Creates a new raw token and its hash.</summary>
    public static (string RawToken, string Hash) Create()
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return (raw, Hash(raw));
    }

    /// <summary>SHA-256 of the raw token as lowercase hex.</summary>
    public static string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawToken);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
