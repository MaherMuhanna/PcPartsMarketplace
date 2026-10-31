using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// Review and place the order: choose an address, check the lines (grouped by seller, cash on delivery),
/// confirm. The page posts the lines as shown; if the API answers 409 (price, stock or cart changed),
/// the customer comes back here with the changes listed and the current numbers on screen.
/// </summary>
[Authorize(Roles = Roles.Customer)]
[Route("checkout")]
public sealed class CheckoutController(
    ICartApiClient cartApi,
    IAddressesApiClient addressesApi,
    IOrdersApiClient ordersApi) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var cart = await cartApi.GetCartAsync(cancellationToken);
        if (cart.ItemCount == 0)
        {
            return RedirectToAction("Index", "Cart");
        }

        if (!cart.CanCheckout)
        {
            TempData["Error"] = "Some items in your cart cannot be bought right now. Fix or remove the highlighted items first.";
            return RedirectToAction("Index", "Cart");
        }

        var addresses = await addressesApi.GetAddressesAsync(cancellationToken: cancellationToken);
        return View(new CheckoutViewModel
        {
            Cart = cart,
            Addresses = addresses.Items,
            SelectedAddressId = addresses.Items.FirstOrDefault(a => a.IsDefault)?.Id ?? addresses.Items.FirstOrDefault()?.Id
        });
    }

    [HttpPost("")]
    public async Task<IActionResult> Place(CheckoutFormModel model, CancellationToken cancellationToken)
    {
        try
        {
            var order = await ordersApi.CheckoutAsync(new CheckoutRequest
            {
                AddressId = model.AddressId,
                Items = model.Items
                    .Select(i => new CheckoutItemRequest { CartItemId = i.CartItemId, UnitPrice = i.UnitPrice, Quantity = i.Quantity })
                    .ToList()
            }, cancellationToken);

            TempData["Success"] = $"Thank you! Order {order.OrderNumber} is placed. Pay in cash when it arrives.";
            return RedirectToAction("Details", "Orders", new { id = order.Id });
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict && ex.Problem?.Items is { Count: > 0 } issues)
        {
            // Show what changed, then the page again with the current prices and stock.
            TempData["Error"] = $"{ex.UserMessage} " + string.Join(" ", issues.Select(i => $"{i.ProductName ?? "An item"}: {i.Message}"));
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            TempData["Error"] = ex.Problem?.Errors?.Values.SelectMany(m => m).FirstOrDefault() ?? ex.UserMessage;
        }

        return RedirectToAction(nameof(Index));
    }
}
