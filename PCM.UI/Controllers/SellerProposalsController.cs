using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Catalog;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// "Product proposals" for approved sellers: propose a product missing from the catalog, follow its review,
/// and fix a rejected one (saving sends it back for review). Approved proposals are read-only: they are
/// catalog products now, and the seller can add an offer. The API enforces approval (403) and ownership (404).
/// </summary>
[Authorize(Roles = Roles.Seller)]
[Route("seller/proposals")]
public sealed class SellerProposalsController(ISellerProposalsApiClient proposalsApi, ProductFormBuilder formBuilder) : Controller
{
    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? sort, int page = 1, CancellationToken cancellationToken = default)
    {
        status = SellerProposalsViewModel.StatusTabs.Contains(status) ? status : null;
        sort = sort is "oldest" or "name" ? sort : null;

        var proposals = await proposalsApi.GetProductsAsync(
            new ProductListFilter(status, null, null, sort, Math.Max(page, 1), PageSize), cancellationToken);
        return View(new SellerProposalsViewModel { Proposals = proposals, Status = status, Sort = sort });
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(int? categoryId, CancellationToken cancellationToken) =>
        View(await formBuilder.NewAsync(ProductFormMode.Seller, categoryId, cancellationToken));

    [HttpPost("create")]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = null;
        if (ModelState.IsValid)
        {
            var built = ProductFormBuilder.ToRequest(model);
            try
            {
                var proposal = await proposalsApi.CreateAsync(built.Request, cancellationToken);
                TempData["Success"] = $"{proposal.Name} was sent for review. You can add an offer once it is approved.";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ProductFormBuilder.TryAddApiErrors(ModelState, ex, built))
            {
                // Shown on the form below.
            }
        }

        model.Mode = ProductFormMode.Seller;
        await formBuilder.PrepareAsync(model, cancellationToken);
        return View(model);
    }

    /// <summary>Edit form for Pending and Rejected proposals; an approved one shows its details instead.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var proposal = await proposalsApi.GetProductAsync(id, cancellationToken);
        if (proposal.Status == "Approved")
        {
            return View("Approved", proposal);
        }

        return View(await formBuilder.FromProductAsync(proposal, ProductFormMode.Seller, cancellationToken));
    }

    [HttpPost("{id:int}")]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            var built = ProductFormBuilder.ToRequest(model);
            try
            {
                var proposal = await proposalsApi.UpdateAsync(id, built.Request, cancellationToken);
                TempData["Success"] = $"{proposal.Name} was saved and is waiting for review.";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ProductFormBuilder.TryAddApiErrors(ModelState, ex, built))
            {
                // Shown on the form below (409 here: approved meanwhile, or duplicate model number).
            }
        }

        model.Mode = ProductFormMode.Seller;
        model.Product = await proposalsApi.GetProductAsync(id, cancellationToken);
        await formBuilder.PrepareAsync(model, cancellationToken);
        return View(model);
    }
}
