using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// "Orders" for approved sellers: their part of each customer order, status tabs, and the buttons for
/// the next step (confirm, ship, deliver) or cancel with a reason. The API decides which moves are allowed.
/// </summary>
[Authorize(Roles = Roles.Seller)]
[Route("seller/orders")]
public sealed class SellerOrdersController(ISellerOrdersApiClient ordersApi) : Controller
{
    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default) =>
        View(new SellerOrdersViewModel
        {
            Orders = await ordersApi.GetOrdersAsync(status, Math.Max(page, 1), PageSize, cancellationToken),
            Status = status
        });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken) =>
        View(await ordersApi.GetOrderAsync(id, cancellationToken));

    [HttpPost("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, string? status, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            var order = await ordersApi.ChangeStatusAsync(id, new ChangeSellerOrderStatusRequest
            {
                Status = status ?? string.Empty,
                Reason = reason
            }, cancellationToken);

            TempData["Success"] = $"Order {order.OrderNumber} is now {order.Status}.";
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            TempData["Error"] = ex.Problem?.Errors?.Values.SelectMany(m => m).FirstOrDefault() ?? ex.UserMessage;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
