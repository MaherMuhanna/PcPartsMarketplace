using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// Admin > Orders: every order, searchable by order number and filterable by shipment status.
/// The details page moves any shipment along the same flow as its seller, or cancels it with a reason.
/// </summary>
[Authorize(Roles = Roles.Admin)]
[Route("admin/orders")]
public sealed class AdminOrdersController(IAdminOrdersApiClient ordersApi) : Controller
{
    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? q, string? sort, int page = 1, CancellationToken cancellationToken = default)
    {
        // Only known values reach the API; anything else means "no filter" instead of a 400 page.
        status = AdminOrdersViewModel.StatusTabs.Contains(status) ? status : null;
        sort = sort == "oldest" ? sort : null;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var orders = await ordersApi.GetOrdersAsync(status, q, sort, Math.Max(page, 1), PageSize, cancellationToken);
        return View(new AdminOrdersViewModel { Orders = orders, Status = status, Q = q, Sort = sort });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken) =>
        View(await ordersApi.GetOrderAsync(id, cancellationToken));

    /// <summary>Moves one shipment (sub-order). Cancelling needs a reason (3-250 characters), checked by the API.</summary>
    [HttpPost("{id:int}/seller-orders/{sellerOrderId:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, int sellerOrderId, string? status, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            var order = await ordersApi.ChangeStatusAsync(id, sellerOrderId, new ChangeSellerOrderStatusRequest
            {
                Status = status ?? string.Empty,
                Reason = reason
            }, cancellationToken);

            var part = order.SellerOrders.FirstOrDefault(p => p.Id == sellerOrderId);
            TempData["Success"] = part is null
                ? $"Order {order.OrderNumber} updated."
                : $"Shipment from {part.StoreName} is now {part.Status}.";
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            // 400: missing or short reason; 409: move not allowed now, or someone else changed it first.
            TempData["Error"] = ex.FirstMessage;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
