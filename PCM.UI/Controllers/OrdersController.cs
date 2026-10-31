using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>The customer's orders: history, details per seller, and cancelling before shipping.</summary>
[Authorize(Roles = Roles.Customer)]
[Route("orders")]
public sealed class OrdersController(IOrdersApiClient ordersApi) : Controller
{
    private const int PageSize = 10;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default) =>
        View(new OrdersViewModel { Orders = await ordersApi.GetOrdersAsync(Math.Max(page, 1), PageSize, cancellationToken) });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken) =>
        View(await ordersApi.GetOrderAsync(id, cancellationToken));

    /// <summary>Cancels every part that has not shipped yet.</summary>
    [HttpPost("{id:int}/cancel")]
    public Task<IActionResult> Cancel(int id, string? reason, CancellationToken cancellationToken) =>
        RunAsync(id, () => ordersApi.CancelOrderAsync(id, new CancelOrderRequest { Reason = reason }, cancellationToken),
            "The order is cancelled.");

    /// <summary>Cancels one seller's part.</summary>
    [HttpPost("{id:int}/seller-orders/{sellerOrderId:int}/cancel")]
    public Task<IActionResult> CancelPart(int id, int sellerOrderId, string? reason, CancellationToken cancellationToken) =>
        RunAsync(id, () => ordersApi.CancelSellerOrderAsync(id, sellerOrderId, new CancelOrderRequest { Reason = reason }, cancellationToken),
            "That part of the order is cancelled.");

    private async Task<IActionResult> RunAsync(int id, Func<Task<OrderDto>> action, string success)
    {
        try
        {
            await action();
            TempData["Success"] = success;
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            // 409: already shipped, or the seller changed it at the same moment.
            TempData["Error"] = ex.Problem?.Errors?.Values.SelectMany(m => m).FirstOrDefault() ?? ex.UserMessage;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
