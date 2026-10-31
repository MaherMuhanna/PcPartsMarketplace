using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>Admin > Brands: add, rename (the slug follows the name), change logo, delete unused brands.</summary>
[Authorize(Roles = Roles.Admin)]
[Route("admin/brands")]
public sealed class AdminBrandsController(IAdminCatalogApiClient catalogApi) : Controller
{
    private const int PageSize = 50;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default) =>
        View(new AdminBrandsViewModel { Brands = await catalogApi.GetBrandsAsync(Math.Max(page, 1), PageSize, "name", cancellationToken) });

    [HttpPost("")]
    public Task<IActionResult> Create(string? name, string? logoUrl, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            var brand = await catalogApi.CreateBrandAsync(ToRequest(name, logoUrl), cancellationToken);
            return $"Brand {brand.Name} added.";
        });

    [HttpPost("{id:int}/edit")]
    public Task<IActionResult> Edit(int id, string? name, string? logoUrl, int page, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            var brand = await catalogApi.UpdateBrandAsync(id, ToRequest(name, logoUrl), cancellationToken);
            return $"Brand {brand.Name} saved.";
        }, page);

    [HttpPost("{id:int}/delete")]
    public Task<IActionResult> Delete(int id, int page, CancellationToken cancellationToken) =>
        RunAsync(async () =>
        {
            await catalogApi.DeleteBrandAsync(id, cancellationToken);
            return "Brand deleted.";
        }, page);

    // ---------------------------------------------------------------------

    private static BrandRequest ToRequest(string? name, string? logoUrl) => new()
    {
        Name = name?.Trim() ?? string.Empty,
        LogoUrl = string.IsNullOrWhiteSpace(logoUrl) ? null : logoUrl.Trim()
    };

    /// <summary>Runs one change and returns to the list with a flash message (400/409 become the error message).</summary>
    private async Task<IActionResult> RunAsync(Func<Task<string>> action, int page = 1)
    {
        try
        {
            TempData["Success"] = await action();
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            // 400: name or logo URL invalid; 409: name taken, or the brand still has products.
            TempData["Error"] = ex.FirstMessage;
        }

        return RedirectToAction(nameof(Index), new { page = page > 1 ? page : (int?)null });
    }
}
