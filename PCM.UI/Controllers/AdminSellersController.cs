using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>Admin > Sellers: review sign-ups and manage seller status.</summary>
[Authorize(Roles = Roles.Admin)]
[Route("admin/sellers")]
public sealed class AdminSellersController(IAdminSellersApiClient sellersApi) : Controller
{
    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page = 1, CancellationToken cancellationToken = default)
    {
        var sellers = await sellersApi.GetSellersAsync(status, Math.Max(page, 1), PageSize, cancellationToken);
        return View(new AdminSellersViewModel { Sellers = sellers, Status = status });
    }

    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id, string? status, CancellationToken cancellationToken) =>
        RunAsync(() => sellersApi.ApproveAsync(id, cancellationToken), "approved", status);

    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, string reason, string? status, CancellationToken cancellationToken) =>
        RunAsync(() => sellersApi.RejectAsync(id, reason ?? string.Empty, cancellationToken), "rejected", status);

    [HttpPost("{id:int}/suspend")]
    public Task<IActionResult> Suspend(int id, string reason, string? status, CancellationToken cancellationToken) =>
        RunAsync(() => sellersApi.SuspendAsync(id, reason ?? string.Empty, cancellationToken), "suspended", status);

    [HttpPost("{id:int}/reinstate")]
    public Task<IActionResult> Reinstate(int id, string? status, CancellationToken cancellationToken) =>
        RunAsync(() => sellersApi.ReinstateAsync(id, cancellationToken), "reinstated", status);

    /// <summary>Runs one status action and returns to the list with a flash message.</summary>
    private async Task<IActionResult> RunAsync(Func<Task<SellerAdminDto>> action, string verb, string? status)
    {
        try
        {
            var seller = await action();
            TempData["Success"] = $"{seller.StoreName} was {verb}.";
        }
        catch (ApiException ex) when (ex.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Conflict)
        {
            // 400: missing or too short reason; 409: action not allowed from the current status.
            TempData["Error"] = ex.Problem?.Errors?.Values.SelectMany(m => m).FirstOrDefault() ?? ex.UserMessage;
        }

        return RedirectToAction(nameof(Index), new { status });
    }
}
