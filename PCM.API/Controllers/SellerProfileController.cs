using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// The signed-in seller's own store profile. Open to sellers in any status, so a
/// pending or suspended seller can see why and keep their details up to date.
/// </summary>
[ApiController]
[Route("api/v1/seller/profile")]
[Authorize(Policy = Policies.SellerAny)]
public sealed class SellerProfileController(ISellerProfileService sellerProfileService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SellerProfileDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SellerProfileDto>> GetProfile(CancellationToken cancellationToken) =>
        Ok(await sellerProfileService.GetMyProfileAsync(User.GetUserId(), cancellationToken));

    /// <summary>Updates description and contact phone. The store name cannot change.</summary>
    [HttpPut]
    [ProducesResponseType<SellerProfileDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SellerProfileDto>> UpdateProfile(UpdateSellerProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await sellerProfileService.UpdateMyProfileAsync(User.GetUserId(), request, cancellationToken));
}
