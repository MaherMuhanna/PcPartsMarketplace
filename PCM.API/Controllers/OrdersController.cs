using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Checkout and the signed-in customer's orders. Other customers' orders return 404.</summary>
[ApiController]
[Route("api/v1/orders")]
[Authorize(Policy = Policies.CustomerOnly)]
public sealed class OrdersController(ICheckoutService checkoutService, ICustomerOrderService orderService) : ControllerBase
{
    /// <summary>
    /// Creates an order from the cart: one sub-order per seller, cash on delivery, stock reserved, cart emptied.
    /// Send the lines as shown to the customer; any change in price, quantity, availability or stock is a 409
    /// whose "items" extension lists the affected lines.
    /// </summary>
    [HttpPost("checkout")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Checkout(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var order = await checkoutService.CheckoutAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpGet]
    [ProducesResponseType<PagedResponse<OrderListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OrderListItemDto>>> GetOrders([FromQuery] OrderListQuery query, CancellationToken cancellationToken) =>
        Ok(await orderService.GetOrdersAsync(User.GetUserId(), query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrder(int id, CancellationToken cancellationToken) =>
        Ok(await orderService.GetOrderAsync(User.GetUserId(), id, cancellationToken));

    /// <summary>Cancels every part of the order that has not shipped yet (Pending or Confirmed). The body (reason) is optional.</summary>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Cancel(
        int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelOrderRequest? request, CancellationToken cancellationToken) =>
        Ok(await orderService.CancelOrderAsync(User.GetUserId(), id, request ?? new CancelOrderRequest(), cancellationToken));

    /// <summary>Cancels one seller's part of the order (Pending or Confirmed only).</summary>
    [HttpPost("{id:int}/seller-orders/{sellerOrderId:int}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> CancelSellerOrder(
        int id, int sellerOrderId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelOrderRequest? request, CancellationToken cancellationToken) =>
        Ok(await orderService.CancelSellerOrderAsync(User.GetUserId(), id, sellerOrderId, request ?? new CancelOrderRequest(), cancellationToken));
}
