namespace PCM.Infrastructure.Catalog;

/// <summary>
/// Query-string keys of GET /api/v1/products that are not spec filters. Spec filters use
/// attribute slugs as keys (e.g. ?memory-type=ddr5), so an attribute may never get one of these slugs.
/// </summary>
public static class CatalogQueryKeys
{
    public const string Keyword = "q";
    public const string Category = "category";
    public const string Brand = "brand";
    public const string MinPrice = "minPrice";
    public const string MaxPrice = "maxPrice";
    public const string Page = "page";
    public const string PageSize = "pageSize";
    public const string Sort = "sort";

    /// <summary>All reserved keys, compared case-insensitively.</summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(
        [Keyword, Category, Brand, MinPrice, MaxPrice, Page, PageSize, Sort],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsReserved(string key) => Reserved.Contains(key);
}
