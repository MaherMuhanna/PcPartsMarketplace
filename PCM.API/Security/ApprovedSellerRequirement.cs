using Microsoft.AspNetCore.Authorization;
using PCM.DomainEntities.Enums;
using PCM.Repositories;

namespace PCM.API.Security;

/// <summary>Policy requirement: the caller is a seller whose profile is Approved right now.</summary>
public sealed class ApprovedSellerRequirement : IAuthorizationRequirement;

/// <summary>
/// Checks the seller status in the database on every request instead of trusting the
/// seller_status claim, so a suspension takes effect immediately, not when the
/// 60-minute access token expires. Pending / Rejected / Suspended sellers get 403.
/// </summary>
public sealed class ApprovedSellerHandler(IUnitOfWork unitOfWork) : AuthorizationHandler<ApprovedSellerRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApprovedSellerRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var status = await unitOfWork.SellerProfiles.GetStatusByUserIdAsync(context.User.GetUserId());
        if (status == SellerStatus.Approved)
        {
            context.Succeed(requirement);
        }
    }
}
