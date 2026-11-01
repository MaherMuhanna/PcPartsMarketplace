using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Caching;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// Seller approval workflow (admins only):
/// Pending -> Approved | Rejected, Approved -> Suspended, Suspended -> Approved (reinstate).
/// An action that does not fit the current status returns 409.
/// </summary>
[ApiController]
[Route("api/v1/admin/sellers")]
[Authorize(Policy = Policies.AdminOnly)]
[InvalidatesCatalogCache(CatalogCacheScope.Products)] // suspend/reinstate hides/shows offers
public sealed class AdminSellersController(ISellerAdminService sellerAdminService) : ControllerBase
{
    /// <summary>Lists sellers, optionally by status. Sort: newest (default), oldest, storeName.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<SellerAdminDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<SellerAdminDto>>> GetSellers([FromQuery] SellerListQuery query, CancellationToken cancellationToken) =>
        Ok(await sellerAdminService.GetSellersAsync(query, cancellationToken));

    [HttpPost("{id:int}/approve")]
    [ProducesResponseType<SellerAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerAdminDto>> Approve(int id, CancellationToken cancellationToken) =>
        Ok(await sellerAdminService.ApproveAsync(id, cancellationToken));

    /// <summary>Rejects a pending seller. The reason is shown to the seller.</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<SellerAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerAdminDto>> Reject(int id, SellerStatusReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await sellerAdminService.RejectAsync(id, request.Reason, cancellationToken));

    /// <summary>Suspends an approved seller and signs them out everywhere.</summary>
    [HttpPost("{id:int}/suspend")]
    [ProducesResponseType<SellerAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerAdminDto>> Suspend(int id, SellerStatusReasonRequest request, CancellationToken cancellationToken) =>
        Ok(await sellerAdminService.SuspendAsync(id, request.Reason, cancellationToken));

    /// <summary>Lifts a suspension (Suspended -> Approved).</summary>
    [HttpPost("{id:int}/reinstate")]
    [ProducesResponseType<SellerAdminDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerAdminDto>> Reinstate(int id, CancellationToken cancellationToken) =>
        Ok(await sellerAdminService.ReinstateAsync(id, cancellationToken));
}
