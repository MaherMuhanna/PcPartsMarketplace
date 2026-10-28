using System.Net;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.UI.ApiClients;
using PCM.UI.Catalog;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// Catalog browsing: category tree, filter sidebar, product grid with sort and paging.
/// The URL is the single source of state (shareable, bookmarkable, works without JavaScript).
/// </summary>
[Route("catalog")]
public sealed class CatalogController(ICatalogApiClient catalogApi) : Controller
{
    public const int PageSize = 12;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var query = CatalogQuery.FromRequest(Request.Query);

        // Form submissions (repeated checkboxes, min/max inputs, empty fields) are
        // redirected once to the short canonical URL, e.g. ?category=ram&memory-type=ddr4,ddr5.
        var canonical = query.ToQueryString();
        var current = Request.QueryString.HasValue ? Request.QueryString.Value![1..] : string.Empty;
        if (!string.Equals(canonical, current, StringComparison.Ordinal))
        {
            return Redirect(query.ToUrl(Url.Action(nameof(Index))!));
        }

        var model = new CatalogViewModel
        {
            Query = query,
            Categories = await catalogApi.GetCategoriesAsync(cancellationToken),
            Filters = await catalogApi.GetFiltersAsync(query.Get("category"), cancellationToken)
        };

        try
        {
            var apiQuery = query.Has("pageSize") ? query : query.With("pageSize", PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
            model.Products = await catalogApi.SearchAsync(apiQuery.ToQueryString(), cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            // A hand-edited URL with a bad value (e.g. capacity=abc): show the message, keep the page usable.
            model.Error = ex.Problem?.Errors is { Count: > 0 } errors
                ? string.Join(" ", errors.SelectMany(e => e.Value))
                : ex.UserMessage;
            model.Products = new PagedResponse<ProductListItemDto>([], 1, PageSize, 0, 0);
        }

        return View(model);
    }
}
