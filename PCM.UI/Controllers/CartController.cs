using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;

namespace PCM.UI.Controllers;

/// <summary>
/// The customer's cart: add from the product page, change quantities, remove, empty.
/// The API owns every rule (stock, 1 to 10 per line, purchasable offers); this controller only
/// shows its messages. Prices on this page are always the current ones.
/// </summary>
[Authorize(Roles = Roles.Customer)]
[Route("cart")]
public sealed class CartController(ICartApiClient cartApi) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await cartApi.GetCartAsync(cancellationToken));

    /// <summary>Posted by the "Add to cart" buttons of the product page; goes back to that page.</summary>
    [HttpPost("add")]
    public async Task<IActionResult> Add(int offerId, int quantity, string? returnUrl, CancellationToken cancellationToken)
    {
        try
        {
            var cart = await cartApi.AddItemAsync(new AddCartItemRequest { OfferId = offerId, Quantity = quantity }, cancellationToken);
            var line = cart.Sellers.SelectMany(s => s.Items).FirstOrDefault(i => i.OfferId == offerId);
            TempData["Success"] = line is null
                ? "Added to your cart."
                : $"Added to your cart: {line.ProductName} ({line.Quantity} in cart).";
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            TempData["Error"] = FirstMessage(ex);
        }

        return RedirectToLocal(returnUrl);
    }

    [HttpPost("items/{id:int}/quantity")]
    public async Task<IActionResult> UpdateQuantity(int id, int quantity, CancellationToken cancellationToken)
    {
        try
        {
            await cartApi.UpdateItemAsync(id, new UpdateCartItemRequest { Quantity = quantity }, cancellationToken);
            TempData["Success"] = "Quantity updated.";
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            TempData["Error"] = FirstMessage(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("items/{id:int}/remove")]
    public async Task<IActionResult> Remove(int id, CancellationToken cancellationToken)
    {
        await cartApi.RemoveItemAsync(id, cancellationToken);
        TempData["Success"] = "Item removed from your cart.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("clear")]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await cartApi.ClearAsync(cancellationToken);
        TempData["Success"] = "Your cart is empty.";
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------------

    private static string FirstMessage(ApiException ex) =>
        ex.Problem?.Errors?.Values.SelectMany(m => m).FirstOrDefault() ?? ex.UserMessage;

    /// <summary>Only same-site paths are followed (blocks open redirects through returnUrl).</summary>
    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
}
