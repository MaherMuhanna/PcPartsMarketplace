using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// The signed-in seller's sub-orders (their part of each customer order). Approved and Suspended sellers
/// (a suspended seller still finishes accepted orders); other sellers' sub-orders return 404.
/// </summary>
[ApiController]
[Route("api/v1/seller/orders")]
[Authorize(Policy = Policies.SellerCanFulfil)]
public sealed class SellerOrdersController(ISellerOrderService orderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<SellerOrderListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<SellerOrderListItemDto>>> GetOrders(
        [FromQuery] SellerOrderListQuery query, CancellationToken cancellationToken) =>
        Ok(await orderService.GetOrdersAsync(User.GetUserId(), query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<SellerOrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SellerOrderDetailsDto>> GetOrder(int id, CancellationToken cancellationToken) =>
        Ok(await orderService.GetOrderAsync(User.GetUserId(), id, cancellationToken));

    /// <summary>
    /// Pending -> Confirmed -> Shipped -> Delivered, or Cancelled (with a reason) before shipping.
    /// Delivered sells the reserved stock; Cancelled releases it.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<SellerOrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SellerOrderDetailsDto>> ChangeStatus(
        int id, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await orderService.ChangeStatusAsync(User.GetUserId(), id, request, cancellationToken));
}
