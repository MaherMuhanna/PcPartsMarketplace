using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PCM.API.Caching;
using PCM.Infrastructure.Catalog;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Public catalog: search with filters, sidebar data and product pages. Only approved, active products.</summary>
[ApiController]
[Route("api/v1/products")]
[OutputCache(PolicyName = CachePolicies.Products)]
public sealed class ProductsController(IProductCatalogService catalogService) : ControllerBase
{
    /// <summary>Spec filters beyond this count are ignored (protects the query from huge URLs).</summary>
    private const int MaxSpecFilters = 20;

    /// <summary>
    /// Searches the catalog.
    /// Known keys: q, category (slug), brand (slugs, comma-separated or repeated), minPrice, maxPrice,
    /// page, pageSize, sort (newest | name | price | -price).
    /// Any other key is a spec filter named after an attribute slug of the (leaf) category:
    /// Enum: memory-type=ddr4,ddr5 (OR). Number: capacity=16-64, capacity=32- or capacity=-64.
    /// Different attributes are combined with AND.
    /// </summary>
    /// <param name="query">The known query keys.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<ProductListItemDto>>> Search(
        [FromQuery] ProductListQuery query,
        CancellationToken cancellationToken)
    {
        // Spec filter keys are dynamic (attribute slugs), so they cannot be bound to a class.
        var specFilters = Request.Query
            .Where(pair => !CatalogQueryKeys.IsReserved(pair.Key))
            .Take(MaxSpecFilters)
            .ToDictionary(pair => pair.Key, pair => string.Join(',', pair.Value.ToArray()), StringComparer.OrdinalIgnoreCase);

        return Ok(await catalogService.SearchAsync(query, specFilters, cancellationToken));
    }

    /// <summary>
    /// What the catalog sidebar can offer: breadcrumb, subcategories, brands with counts and,
    /// for a leaf category, its filterable attributes (options with counts, number ranges).
    /// </summary>
    /// <param name="category">Category slug; omit for the whole catalog.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet("filters")]
    [ProducesResponseType<CatalogFiltersDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CatalogFiltersDto>> GetFilters([FromQuery] string? category, CancellationToken cancellationToken) =>
        Ok(await catalogService.GetFiltersAsync(category, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await catalogService.GetByIdAsync(id, cancellationToken));

    [HttpGet("by-slug/{slug}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDetailsDto>> GetBySlug(string slug, CancellationToken cancellationToken) =>
        Ok(await catalogService.GetBySlugAsync(slug, cancellationToken));
}
