using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Catalog;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// Admin > Products: every master product in any status, the review queue of seller proposals
/// (status = Pending), create and edit with the shared product form, show/hide, approve and reject.
/// </summary>
[Authorize(Roles = Roles.Admin)]
[Route("admin/products")]
public sealed class AdminProductsController(
    IAdminProductsApiClient productsApi,
    ICatalogApiClient catalogApi,
    ProductFormBuilder formBuilder) : Controller
{
    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int? categoryId, string? q, string? sort, int page = 1, CancellationToken cancellationToken = default)
    {
        // Unknown values mean "no filter" instead of a 400 page.
        status = AdminProductsViewModel.StatusTabs.Contains(status) ? status : null;
        sort = sort is "oldest" or "name" ? sort : null;
        categoryId = categoryId > 0 ? categoryId : null;
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

        var products = await productsApi.GetProductsAsync(
            new ProductListFilter(status, categoryId, q, sort, Math.Max(page, 1), PageSize), cancellationToken);
        var tree = await catalogApi.GetCategoriesAsync(cancellationToken);

        return View(new AdminProductsViewModel
        {
            Products = products,
            Status = status,
            CategoryId = categoryId,
            Q = q,
            Sort = sort,
            Categories = tree.Leaves()
        });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(int? categoryId, CancellationToken cancellationToken) =>
        View(await formBuilder.NewAsync(ProductFormMode.Admin, categoryId, cancellationToken));

    [HttpPost("create")]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        await formBuilder.BindSpecsAsync(model, cancellationToken);
        if (ModelState.IsValid)
        {
            var built = ProductFormBuilder.ToRequest(model);
            try
            {
                var product = await productsApi.CreateAsync(built.Request, cancellationToken);
                TempData["Success"] = $"{product.Name} was created and is live in the catalog.";
                return RedirectToAction(nameof(Edit), new { id = product.Id });
            }
            catch (ApiException ex) when (ProductFormBuilder.TryAddApiErrors(ModelState, ex, built))
            {
                // Shown on the form below.
            }
        }

        model.Mode = ProductFormMode.Admin;
        await formBuilder.PrepareAsync(model, cancellationToken);
        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var product = await productsApi.GetProductAsync(id, cancellationToken);
        return View(await formBuilder.FromProductAsync(product, ProductFormMode.Admin, cancellationToken));
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        await formBuilder.BindSpecsAsync(model, cancellationToken);
        if (ModelState.IsValid)
        {
            var built = ProductFormBuilder.ToRequest(model);
            try
            {
                var product = await productsApi.UpdateAsync(id, built.Request, cancellationToken);
                TempData["Success"] = $"{product.Name} was saved.";
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (ApiException ex) when (ProductFormBuilder.TryAddApiErrors(ModelState, ex, built))
            {
                // Shown on the form below.
            }
        }

        model.Mode = ProductFormMode.Admin;
        model.Product = await productsApi.GetProductAsync(id, cancellationToken);
        await formBuilder.PrepareAsync(model, cancellationToken);
        return View(model);
    }

    // ---- Status actions (from the list or the edit page; return to where they came from) ----

    [HttpPost("{id:int}/activate")]
    public Task<IActionResult> Activate(int id, string? returnUrl, CancellationToken cancellationToken) =>
        RunAsync(async () => $"{(await productsApi.SetActiveAsync(id, true, cancellationToken)).Name} is visible in the catalog again.", returnUrl);

    [HttpPost("{id:int}/deactivate")]
    public Task<IActionResult> Deactivate(int id, string? returnUrl, CancellationToken cancellationToken) =>
        RunAsync(async () => $"{(await productsApi.SetActiveAsync(id, false, cancellationToken)).Name} is hidden from the catalog.", returnUrl);

    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id, string? returnUrl, CancellationToken cancellationToken) =>
        RunAsync(async () => $"{(await productsApi.ApproveAsync(id, cancellationToken)).Name} was approved and is now in the catalog.", returnUrl);

    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id, string? note, string? returnUrl, CancellationToken cancellationToken) =>
        RunAsync(async () => $"{(await productsApi.RejectAsync(id, note?.Trim() ?? string.Empty, cancellationToken)).Name} was rejected. The seller sees your note.", returnUrl);

    // ---------------------------------------------------------------------

    private async Task<IActionResult> RunAsync(Func<Task<string>> action, string? returnUrl)
    {
        try
        {
            TempData["Success"] = await action();
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            // 400: note too short; 409: only Pending proposals can be approved or rejected.
            TempData["Error"] = ex.FirstMessage;
        }

        // Only same-site paths are followed (blocks open redirects through returnUrl).
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }
}
