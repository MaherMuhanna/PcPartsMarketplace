using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Extensions;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// "My offers" for approved sellers: list with stock tabs, add an offer for a catalog product,
/// edit price and low-stock threshold, change stock, delete, and the stock history.
/// The API enforces approval (403 -> Access denied page) and ownership (404 -> Not found page).
/// </summary>
[Authorize(Roles = Roles.Seller)]
[Route("seller/offers")]
public sealed class SellerOffersController(ISellerOffersApiClient offersApi, ICatalogApiClient catalogApi) : Controller
{
    private const int PageSize = 20;
    private const int PickerPageSize = 12;
    private const int HistoryPageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, string? condition, string? stock, string? sort, int page = 1, CancellationToken cancellationToken = default)
    {
        var offers = await offersApi.GetOffersAsync(
            new OfferListFilter(q, condition, stock, sort, Math.Max(page, 1), PageSize), cancellationToken);

        return View(new SellerOffersViewModel { Offers = offers, Q = q, Condition = condition, Stock = stock, Sort = sort });
    }

    /// <summary>Without a product: search the catalog. With ?productId=: the offer form for that product.</summary>
    [HttpGet("create")]
    public async Task<IActionResult> Create(int? productId, string? q, CancellationToken cancellationToken)
    {
        if (productId is { } id)
        {
            return View(new OfferCreateViewModel { ProductId = id, Product = await catalogApi.GetProductByIdAsync(id, cancellationToken) });
        }

        var results = string.IsNullOrWhiteSpace(q)
            ? null
            : await catalogApi.SearchAsync($"q={Uri.EscapeDataString(q.Trim())}&pageSize={PickerPageSize}&sort=name", cancellationToken);

        return View("Pick", new OfferProductPickerViewModel { Q = q, Results = results });
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(OfferCreateViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var offer = await offersApi.CreateAsync(new CreateOfferRequest
                {
                    ProductId = model.ProductId,
                    Condition = model.Condition,
                    Price = model.Price,
                    Stock = model.Stock,
                    LowStockThreshold = model.LowStockThreshold
                }, cancellationToken);

                TempData["Success"] = $"Offer added: {offer.ProductName} ({offer.Condition}).";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ModelState.TryAddApiError(ex))
            {
                // Shown on the form below.
            }
        }

        model.Product = await catalogApi.GetProductByIdAsync(model.ProductId, cancellationToken);
        return View(model);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken) =>
        View(OfferEditViewModel.From(await offersApi.GetOfferAsync(id, cancellationToken)));

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, OfferEditViewModel model, CancellationToken cancellationToken)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            try
            {
                await offersApi.UpdateAsync(id, new UpdateOfferRequest { Price = model.Price, LowStockThreshold = model.LowStockThreshold }, cancellationToken);
                TempData["Success"] = "Offer updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (ApiException ex) when (ModelState.TryAddApiError(ex))
            {
                // Shown on the form below.
            }
        }

        model.Offer = await offersApi.GetOfferAsync(id, cancellationToken);
        return View(model);
    }

    /// <summary>Posted by the stock modal (list and history pages); returns to the page it came from.</summary>
    [HttpPost("{id:int}/stock")]
    public async Task<IActionResult> Stock(int id, string? type, int change, string? note, string? returnUrl, CancellationToken cancellationToken)
    {
        try
        {
            var offer = await offersApi.ChangeStockAsync(id, new StockChangeRequest
            {
                Type = type ?? string.Empty,
                Change = change,
                Note = note
            }, cancellationToken);

            TempData["Success"] = $"Stock of {offer.ProductName} is now {offer.Stock} ({offer.Available} available).";
        }
        catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            // 400: bad quantity or missing note; 409: below reserved stock, or changed by someone else twice.
            TempData["Error"] = FirstMessage(ex);
        }

        return RedirectToLocal(returnUrl);
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            await offersApi.DeleteAsync(id, cancellationToken);
            TempData["Success"] = "Offer deleted.";
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            TempData["Error"] = FirstMessage(ex);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> History(int id, int page = 1, CancellationToken cancellationToken = default)
    {
        var offer = await offersApi.GetOfferAsync(id, cancellationToken);
        var movements = await offersApi.GetMovementsAsync(id, Math.Max(page, 1), HistoryPageSize, cancellationToken);
        return View(new OfferHistoryViewModel { Offer = offer, Movements = movements });
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
