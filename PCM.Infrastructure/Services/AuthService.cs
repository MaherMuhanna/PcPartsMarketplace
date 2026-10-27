using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Security;
using PCM.Repositories;

namespace PCM.Infrastructure.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    IAccessTokenFactory accessTokenFactory,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    // One message for "unknown email" and "wrong password", so the API does not reveal which emails exist.
    private const string InvalidCredentials = "Invalid email or password.";
    private const string LockedOut = "Too many failed attempts. The account is locked for 15 minutes.";
    private const string InvalidRefreshToken = "The refresh token is invalid or expired. Please sign in again.";

    /// <summary>CreatedByIp is varchar(45): enough for any IPv6 address.</summary>
    private const int MaxIpLength = 45;

    public async Task<AuthResponse> RegisterCustomerAsync(RegisterCustomerRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        await EnsureEmailAvailableAsync(email);

        var user = new ApplicationUser { UserName = email, Email = email, FullName = request.FullName.Trim() };

        // User, role and first refresh token are saved together or not at all.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await CreateUserAsync(user, request.Password, Roles.Customer);
        var (response, _) = await IssueTokensAsync(user, [Roles.Customer], sellerStatus: null, ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Customer {UserId} registered", user.Id);
        return response;
    }

    public async Task<AuthResponse> RegisterSellerAsync(RegisterSellerRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var storeName = request.StoreName.Trim();

        await EnsureEmailAvailableAsync(email);
        if (await unitOfWork.SellerProfiles.ExistsAsync(s => s.StoreName == storeName, cancellationToken))
        {
            throw new ConflictException($"The store name '{storeName}' is already taken.");
        }

        var user = new ApplicationUser { UserName = email, Email = email, FullName = request.FullName.Trim() };

        // Identity user (AuthDbContext) and SellerProfile (PcPartsContext) in one transaction.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await CreateUserAsync(user, request.Password, Roles.Seller);

        await unitOfWork.SellerProfiles.AddAsync(new SellerProfile
        {
            UserId = user.Id,
            StoreName = storeName,
            Description = NullIfBlank(request.Description),
            ContactPhone = NullIfBlank(request.ContactPhone),
            Status = nameof(SellerStatus.Pending)
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var (response, _) = await IssueTokensAsync(user, [Roles.Seller], SellerStatus.Pending, ipAddress, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Seller {UserId} registered with store {StoreName}, pending approval", user.Id, storeName);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim())
            ?? throw new UnauthorizedException(InvalidCredentials);

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(LockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Counts the failure; the 5th failure in a row locks the account (Identity lockout options).
            await userManager.AccessFailedAsync(user);
            logger.LogInformation("Failed login for user {UserId}", user.Id);

            throw new UnauthorizedException(await userManager.IsLockedOutAsync(user) ? LockedOut : InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        // Pending, Rejected and Suspended sellers can sign in (agreed): selling endpoints check
        // the ApprovedSeller policy instead, and the UI shows the status banner.
        var (roles, sellerStatus) = await GetRolesAndSellerStatusAsync(user, cancellationToken);
        var (response, _) = await IssueTokensAsync(user, roles, sellerStatus, ipAddress, cancellationToken);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var now = UtcNow();
        var stored = await unitOfWork.RefreshTokens.GetByHashAsync(RefreshTokenGenerator.Hash(refreshToken), cancellationToken)
            ?? throw new UnauthorizedException(InvalidRefreshToken);

        if (stored.RevokedAt is not null)
        {
            if (stored.ReplacedByTokenId is not null)
            {
                // A rotated token came back: someone else may hold a copy. Kill everything issued after it.
                await HandleReuseAsync(stored, now, cancellationToken);
            }

            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (stored.ExpiresAt <= now)
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        var (roles, sellerStatus) = await GetRolesAndSellerStatusAsync(user, cancellationToken);

        // Rotation: insert the new token, then revoke the old one only if it is still active.
        // If a concurrent request rotated it first, the conditional update affects 0 rows,
        // the new token is rolled back and the request is treated as reuse.
        AuthResponse response;
        await using (var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken))
        {
            (response, var newToken) = await IssueTokensAsync(user, roles, sellerStatus, ipAddress, cancellationToken);

            if (!await unitOfWork.RefreshTokens.TryRevokeAsync(stored.Id, now, newToken.Id, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                await HandleReuseAsync(stored, now, cancellationToken);
                throw new UnauthorizedException(InvalidRefreshToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        return response;
    }

    public async Task LogoutAsync(int userId, string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await unitOfWork.RefreshTokens.GetByHashAsync(RefreshTokenGenerator.Hash(refreshToken), cancellationToken);

        // Someone else's or an unknown token is ignored silently: logout never reveals token ownership.
        if (stored is null || stored.UserId != userId)
        {
            return;
        }

        await unitOfWork.RefreshTokens.TryRevokeAsync(stored.Id, UtcNow(), replacedByTokenId: null, cancellationToken);
    }

    public async Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User not found.");

        var (roles, sellerStatus) = await GetRolesAndSellerStatusAsync(user, cancellationToken);
        return ToUserDto(user, roles, sellerStatus);
    }

    // ---------------------------------------------------------------------

    private async Task EnsureEmailAvailableAsync(string email)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }
    }

    /// <summary>Creates the Identity user and assigns the role; Identity errors become HTTP-friendly exceptions.</summary>
    private async Task CreateUserAsync(ApplicationUser user, string password, string role)
    {
        ThrowIfFailed(await userManager.CreateAsync(user, password));
        ThrowIfFailed(await userManager.AddToRoleAsync(user, role));
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return;
        }

        // Duplicate email can still happen in a race between the pre-check and the insert.
        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        // Other Identity rules (password policy) are already enforced by FluentValidation,
        // so reaching this point is a programming or configuration error.
        throw new InvalidOperationException("Identity rejected the user: " + string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private async Task<(IReadOnlyList<string> Roles, SellerStatus? SellerStatus)> GetRolesAndSellerStatusAsync(
        ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = (await userManager.GetRolesAsync(user)).ToList();

        SellerStatus? sellerStatus = roles.Contains(Roles.Seller)
            ? await unitOfWork.SellerProfiles.GetStatusByUserIdAsync(user.Id, cancellationToken)
            : null;

        return (roles, sellerStatus);
    }

    /// <summary>Creates an access token and a new refresh token (saved immediately so its Id is known).</summary>
    private async Task<(AuthResponse Response, RefreshToken Entity)> IssueTokensAsync(
        ApplicationUser user,
        IReadOnlyList<string> roles,
        SellerStatus? sellerStatus,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var accessToken = accessTokenFactory.Create(user, roles, sellerStatus);
        var (rawRefreshToken, hash) = RefreshTokenGenerator.Create();

        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays),
            CreatedByIp = ipAddress is { Length: > MaxIpLength } ? ipAddress[..MaxIpLength] : ipAddress
        };

        await unitOfWork.RefreshTokens.AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = new AuthResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            rawRefreshToken,
            entity.ExpiresAt,
            ToUserDto(user, roles, sellerStatus));

        return (response, entity);
    }

    /// <summary>
    /// Reuse detection: walks the rotation chain (ReplacedByTokenId) from the reused token
    /// and revokes every descendant that is still active, forcing whoever holds them to sign in again.
    /// </summary>
    private async Task HandleReuseAsync(RefreshToken reused, DateTime now, CancellationToken cancellationToken)
    {
        // Re-read: in the concurrent case the successor was written by the other request.
        var current = await unitOfWork.RefreshTokens.GetByHashAsync(reused.TokenHash, cancellationToken);
        var nextId = current?.ReplacedByTokenId;
        var revoked = 0;

        // The guard stops a corrupt (cyclic) chain from looping forever.
        for (var guard = 0; nextId is not null && guard < 1000; guard++)
        {
            var token = await unitOfWork.RefreshTokens.GetByIdAsync(nextId.Value, cancellationToken);
            if (token is null)
            {
                break;
            }

            if (token.RevokedAt is null)
            {
                token.RevokedAt = now;
                revoked++;
            }

            nextId = token.ReplacedByTokenId;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Refresh token reuse detected for user {UserId} (token {TokenId}); revoked {Count} descendant token(s)",
            reused.UserId, reused.Id, revoked);
    }

    private static UserDto ToUserDto(ApplicationUser user, IReadOnlyList<string> roles, SellerStatus? sellerStatus) =>
        new(user.Id, user.FullName, user.Email ?? string.Empty, roles, sellerStatus?.ToString());

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;
}
