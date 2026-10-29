using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.Services;

/// <summary>The public, read-only catalog: search, filters and product pages.</summary>
public interface IProductCatalogService
{
    /// <summary>
    /// Searches published products.
    /// <paramref name="specFilters"/> maps attribute slugs to raw values: "ddr4,ddr5" for an
    /// Enum attribute, "16-64" / "16-" / "-64" / "32" for a Number attribute. They apply only when
    /// the category is a leaf; unknown keys are ignored. 404 for an unknown category slug,
    /// 400 for a malformed number range.
    /// </summary>
    Task<PagedResponse<ProductListItemDto>> SearchAsync(
        ProductListQuery query,
        IReadOnlyDictionary<string, string> specFilters,
        CancellationToken cancellationToken = default);

    /// <summary>Sidebar data for a category slug (null = whole catalog). 404 for an unknown slug.</summary>
    Task<CatalogFiltersDto> GetFiltersAsync(string? categorySlug, CancellationToken cancellationToken = default);

    /// <summary>A published product with specs, images and offers; 404 if missing or not published.</summary>
    Task<ProductDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="GetByIdAsync"/>, by slug.</summary>
    Task<ProductDetailsDto> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
