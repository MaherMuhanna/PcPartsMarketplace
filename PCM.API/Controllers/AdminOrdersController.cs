using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Every order, for admins. The UI for these endpoints comes in Phase 6 (use Scalar for now).</summary>
[ApiController]
[Route("api/v1/admin/orders")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminOrdersController(IAdminOrderService orderService) : ControllerBase
{
    /// <summary>Filter by sub-order status and part of the order number; newest first by default.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<OrderListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OrderListItemDto>>> GetOrders(
        [FromQuery] AdminOrderListQuery query, CancellationToken cancellationToken) =>
        Ok(await orderService.GetOrdersAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrder(int id, CancellationToken cancellationToken) =>
        Ok(await orderService.GetOrderAsync(id, cancellationToken));

    /// <summary>Any allowed move on one sub-order, e.g. cancelling the open orders of a suspended seller (reason required).</summary>
    [HttpPatch("{id:int}/seller-orders/{sellerOrderId:int}/status")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> ChangeStatus(
        int id, int sellerOrderId, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await orderService.ChangeStatusAsync(User.GetUserId(), id, sellerOrderId, request, cancellationToken));
}
