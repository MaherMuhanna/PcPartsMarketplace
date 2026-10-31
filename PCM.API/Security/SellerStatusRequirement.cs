using Microsoft.AspNetCore.Authorization;
using PCM.DomainEntities.Enums;
using PCM.Repositories;

namespace PCM.API.Security;

/// <summary>
/// Policy requirement: the caller is a seller whose profile status is one of <see cref="AllowedStatuses"/> right now.
/// Used by <c>ApprovedSeller</c> (selling) and <c>SellerCanFulfil</c> (finishing existing sub-orders).
/// </summary>
public sealed class SellerStatusRequirement(params SellerStatus[] allowedStatuses) : IAuthorizationRequirement
{
    public IReadOnlyCollection<SellerStatus> AllowedStatuses { get; } = allowedStatuses;
}

/// <summary>
/// Checks the seller status in the database on every request instead of trusting the
/// seller_status claim, so a suspension takes effect immediately, not when the
/// 60-minute access token expires. Any other status gets 403.
/// </summary>
public sealed class SellerStatusHandler(IUnitOfWork unitOfWork) : AuthorizationHandler<SellerStatusRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SellerStatusRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var status = await unitOfWork.SellerProfiles.GetStatusByUserIdAsync(context.User.GetUserId());
        if (status is { } current && requirement.AllowedStatuses.Contains(current))
        {
            context.Succeed(requirement);
        }
    }
}
