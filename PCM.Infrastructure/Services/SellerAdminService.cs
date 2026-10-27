using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;
using PCM.Repositories.IRepository;

namespace PCM.Infrastructure.Services;

public sealed class SellerAdminService(
    IUnitOfWork unitOfWork,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider,
    ILogger<SellerAdminService> logger) : ISellerAdminService
{
    public async Task<PagedResponse<SellerAdminDto>> GetSellersAsync(SellerListQuery query, CancellationToken cancellationToken = default)
    {
        SellerStatus? status = query.Status is null ? null : Enum.Parse<SellerStatus>(query.Status, ignoreCase: true);

        var page = await unitOfWork.SellerProfiles.GetPagedAsync(status, query.Page, query.PageSize, ParseSort(query.Sort), cancellationToken);

        // Owner name and email live in AspNetUsers (AuthDbContext): one extra query for the whole page.
        var userIds = page.Items.Select(s => s.UserId).ToList();
        var owners = await userManager.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return page.ToResponse(profile => owners.TryGetValue(profile.UserId, out var owner)
            ? profile.ToAdminDto(owner.FullName, owner.Email ?? string.Empty)
            : profile.ToAdminDto(string.Empty, string.Empty));
    }

    public Task<SellerAdminDto> ApproveAsync(int sellerId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(sellerId, SellerAction.Approve, reason: null, cancellationToken);

    public Task<SellerAdminDto> RejectAsync(int sellerId, string reason, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(sellerId, SellerAction.Reject, reason, cancellationToken);

    public Task<SellerAdminDto> SuspendAsync(int sellerId, string reason, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(sellerId, SellerAction.Suspend, reason, cancellationToken);

    public Task<SellerAdminDto> ReinstateAsync(int sellerId, CancellationToken cancellationToken = default) =>
        ChangeStatusAsync(sellerId, SellerAction.Reinstate, reason: null, cancellationToken);

    // ---------------------------------------------------------------------

    private async Task<SellerAdminDto> ChangeStatusAsync(int sellerId, SellerAction action, string? reason, CancellationToken cancellationToken)
    {
        var profile = await unitOfWork.SellerProfiles.GetByIdAsync(sellerId, cancellationToken)
            ?? throw new NotFoundException($"Seller {sellerId} was not found.");

        var current = Enum.Parse<SellerStatus>(profile.Status);
        var next = SellerStatusTransitions.Apply(current, action);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        profile.Status = next.ToString();
        profile.StatusReason = reason?.Trim();          // cleared on approve / reinstate
        if (next == SellerStatus.Approved)
        {
            profile.ApprovedAt ??= now;                 // first approval time is kept on reinstate
        }

        // Rejected or suspended: end the seller's sessions. Access tokens expire on their own
        // (60 min) and selling endpoints re-check the status in the database on every request.
        var revokedTokens = 0;
        if (next is SellerStatus.Rejected or SellerStatus.Suspended)
        {
            foreach (var token in await unitOfWork.RefreshTokens.GetActiveByUserAsync(profile.UserId, now, cancellationToken))
            {
                token.RevokedAt = now;
                revokedTokens++;
            }
        }

        // Status change and token revocation are saved together.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seller {SellerId} changed from {From} to {To} ({Action}); revoked {Count} refresh token(s)",
            profile.Id, current, next, action, revokedTokens);

        var owner = await userManager.FindByIdAsync(profile.UserId.ToString());
        return profile.ToAdminDto(owner?.FullName ?? string.Empty, owner?.Email ?? string.Empty);
    }

    private static SellerSort ParseSort(string? sort) => sort?.ToLowerInvariant() switch
    {
        "oldest" => SellerSort.Oldest,
        "storename" => SellerSort.StoreName,
        _ => SellerSort.Newest
    };
}
