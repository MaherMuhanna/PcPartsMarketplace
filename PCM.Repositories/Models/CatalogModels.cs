namespace PCM.Repositories.Models;

// Read models returned by catalog queries. They are projections (no tracking,
// no navigation properties), shaped for the queries that produce them.

/// <summary>
/// Everything the public product search can filter on. All filters are combined with AND;
/// inside one spec filter the listed options are combined with OR.
/// </summary>
public sealed record ProductSearchCriteria
{
    /// <summary>Category scope (a category plus its descendants). Null = whole catalog.</summary>
    public IReadOnlyCollection<int>? CategoryIds { get; init; }

    /// <summary>Brand ids (OR). Null or empty = any brand.</summary>
    public IReadOnlyCollection<int>? BrandIds { get; init; }

    /// <summary>Matched with LIKE against Name and ModelNumber.</summary>
    public string? Keyword { get; init; }

    /// <summary>Lowest available offer price must be at least this.</summary>
    public decimal? MinPrice { get; init; }

    /// <summary>Lowest available offer price must be at most this.</summary>
    public decimal? MaxPrice { get; init; }

    public IReadOnlyList<OptionFilter> OptionFilters { get; init; } = [];

    public IReadOnlyList<NumberFilter> NumberFilters { get; init; } = [];
}

/// <summary>The product must have one of <paramref name="OptionIds"/> for the attribute.</summary>
public sealed record OptionFilter(int AttributeId, IReadOnlyCollection<int> OptionIds);

/// <summary>
/// The product's number for the attribute must be one of <paramref name="Values"/> (when given),
/// otherwise inside the inclusive range Min..Max (a null bound is open).
/// </summary>
public sealed record NumberFilter(int AttributeId, decimal? Min, decimal? Max, IReadOnlyCollection<decimal>? Values = null);

/// <summary>Sort orders of the public product search.</summary>
public enum ProductSort
{
    Newest,
    Name,
    PriceAsc,
    PriceDesc
}

/// <summary>One product card in the catalog.</summary>
public sealed record ProductListRow(
    int Id,
    string Name,
    string Slug,
    string ModelNumber,
    int BrandId,
    string BrandName,
    string BrandSlug,
    int CategoryId,
    string CategoryName,
    string CategorySlug,
    string? MainImageUrl,
    decimal? MinPrice,
    int OfferCount,
    bool InStock);

/// <summary>A visible offer on the product page (approved seller, not deleted).</summary>
public sealed record ProductOfferRow(
    int OfferId,
    int SellerId,
    string StoreName,
    string Condition,
    decimal Price,
    int Available);

/// <summary>How many catalog products have a given value (brand id or option id).</summary>
public sealed record FacetCount(int Id, int Count);

/// <summary>How many catalog products have a given value of a Number attribute (e.g. Capacity 32: 3 products).</summary>
public sealed record NumberFacet(int AttributeId, decimal Value, int Count);

/// <summary>Filters of the admin and seller product lists (any status).</summary>
public sealed record ProductManageFilter
{
    public string? Status { get; init; }

    public int? CategoryId { get; init; }

    public string? Keyword { get; init; }

    /// <summary>Only proposals of this seller (seller "my proposals" list).</summary>
    public int? ProposedBySellerId { get; init; }
}

/// <summary>Sort orders of the admin and seller product lists.</summary>
public enum ProductManageSort
{
    Newest,
    Oldest,
    Name
}
