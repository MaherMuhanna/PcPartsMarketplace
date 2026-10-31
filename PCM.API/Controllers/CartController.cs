using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// The signed-in customer's cart. One cart per customer, created on the first add; lines reference
/// offers and always show the current price and stock. Other customers' lines return 404.
/// </summary>
[ApiController]
[Route("api/v1/cart")]
[Authorize(Policy = Policies.CustomerOnly)]
public sealed class CartController(ICartService cartService) : ControllerBase
{
    /// <summary>Lines grouped by seller, with issues (unavailable, not enough stock) and totals.</summary>
    [HttpGet]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CartDto>> GetCart(CancellationToken cancellationToken) =>
        Ok(await cartService.GetCartAsync(User.GetUserId(), cancellationToken));

    /// <summary>Total units in the cart (navigation badge).</summary>
    [HttpGet("summary")]
    [ProducesResponseType<CartSummaryDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CartSummaryDto>> GetSummary(CancellationToken cancellationToken) =>
        Ok(await cartService.GetSummaryAsync(User.GetUserId(), cancellationToken));

    /// <summary>Adds an offer (or more units of an offer already in the cart). Returns the whole cart.</summary>
    [HttpPost("items")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartDto>> AddItem(AddCartItemRequest request, CancellationToken cancellationToken) =>
        Ok(await cartService.AddItemAsync(User.GetUserId(), request, cancellationToken));

    /// <summary>Sets the quantity of a line. Returns the whole cart.</summary>
    [HttpPut("items/{id:int}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CartDto>> UpdateItem(int id, UpdateCartItemRequest request, CancellationToken cancellationToken) =>
        Ok(await cartService.UpdateItemAsync(User.GetUserId(), id, request, cancellationToken));

    [HttpDelete("items/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(int id, CancellationToken cancellationToken)
    {
        await cartService.RemoveItemAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    /// <summary>Empties the cart.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await cartService.ClearAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
