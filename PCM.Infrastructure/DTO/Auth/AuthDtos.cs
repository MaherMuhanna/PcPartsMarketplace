namespace PCM.Infrastructure.DTO.Auth;

/// <summary>POST /api/v1/auth/register/customer</summary>
public sealed record RegisterCustomerRequest
{
    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

/// <summary>POST /api/v1/auth/register/seller. The seller starts as Pending until an admin approves.</summary>
public sealed record RegisterSellerRequest
{
    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string StoreName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? ContactPhone { get; init; }
}

/// <summary>POST /api/v1/auth/login</summary>
public sealed record LoginRequest
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

/// <summary>POST /api/v1/auth/refresh and POST /api/v1/auth/logout</summary>
public sealed record RefreshTokenRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// The signed-in user, as returned by login, refresh and GET /auth/me.
/// SellerStatus is Pending / Approved / Rejected / Suspended for sellers, null otherwise.
/// </summary>
public sealed record UserDto(
    int Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    string? SellerStatus);

/// <summary>Tokens issued by register, login and refresh. All times are UTC.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
