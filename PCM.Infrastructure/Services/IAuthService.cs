using PCM.Infrastructure.DTO.Auth;

namespace PCM.Infrastructure.Services;

/// <summary>Registration, login and the refresh-token lifecycle.</summary>
public interface IAuthService
{
    /// <summary>Creates a Customer account and signs it in. 409 if the email is taken.</summary>
    Task<AuthResponse> RegisterCustomerAsync(RegisterCustomerRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Seller account with a Pending SellerProfile (one transaction) and signs it in.
    /// 409 if the email or store name is taken.
    /// </summary>
    Task<AuthResponse> RegisterSellerAsync(RegisterSellerRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Checks the credentials (with lockout) and issues tokens. 401 on failure.</summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the refresh token: the old one is revoked and linked to its replacement.
    /// Presenting an already-rotated token revokes every token issued after it (reuse detection). 401 on failure.
    /// </summary>
    Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Revokes the refresh token if it belongs to the user. Always succeeds (idempotent).</summary>
    Task LogoutAsync(int userId, string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Fresh account details of the signed-in user (roles and seller status from the database).</summary>
    Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default);
}
