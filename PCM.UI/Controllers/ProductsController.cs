using Microsoft.AspNetCore.Mvc;
using PCM.UI.ApiClients;

namespace PCM.UI.Controllers;

/// <summary>Product page: specs, images and the offers of approved sellers (cheapest first).</summary>
[Route("products")]
public sealed class ProductsController(ICatalogApiClient catalogApi) : Controller
{
    /// <summary>Unknown or unpublished products show the Not Found page (ApiExceptionFilter).</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken) =>
        View(await catalogApi.GetProductBySlugAsync(slug, cancellationToken));
}
