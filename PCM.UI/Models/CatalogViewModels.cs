using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.UI.Catalog;

namespace PCM.UI.Models;

public sealed class CatalogViewModel
{
    public static readonly IReadOnlyList<(string Value, string Label)> SortOptions =
    [
        ("newest", "Newest"),
        ("name", "Name (A to Z)"),
        ("price", "Price: low to high"),
        ("-price", "Price: high to low")
    ];

    public CatalogQuery Query { get; set; } = CatalogQuery.Empty;

    public IReadOnlyList<CategoryTreeDto> Categories { get; set; } = [];

    public CatalogFiltersDto Filters { get; set; } = new(null, false, [], [], [], []);

    public PagedResponse<ProductListItemDto> Products { get; set; } = new([], 1, 12, 0, 0);

    /// <summary>Message for a bad filter value typed into the URL.</summary>
    public string? Error { get; set; }

    public string Title => Filters.Category?.Name ?? (Query.Has("q") ? $"Results for \"{Query.Get("q")}\"" : "All products");

    /// <summary>Is any filter (other than category, sort and paging) applied?</summary>
    public bool HasActiveFilters =>
        Query.Has("q") || Query.Has("brand") || Query.Has("minPrice") || Query.Has("maxPrice") || Query.SpecKeys.Any();

    /// <summary>Link to the catalog with the category changed and every other filter cleared (except the keyword).</summary>
    public string CategoryUrl(string path, string? slug) =>
        CatalogQuery.Empty.With("q", Query.Get("q")).With("category", slug).ToUrl(path);

    public string PageUrl(string path, int page) =>
        Query.With("page", page == 1 ? null : page.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToUrl(path);
}
