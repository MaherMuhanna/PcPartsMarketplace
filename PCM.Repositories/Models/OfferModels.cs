namespace PCM.Repositories.Models;

// Read models for the seller's offer screens. Projections only (no tracking).

/// <summary>Stock level filter of the seller's offer list. Available = Stock - ReservedStock.</summary>
public enum OfferStockFilter
{
    /// <summary>Every offer.</summary>
    All,

    /// <summary>Available &gt; 0.</summary>
    InStock,

    /// <summary>Available &lt;= LowStockThreshold (includes out of stock).</summary>
    Low,

    /// <summary>Available = 0.</summary>
    OutOfStock
}

/// <summary>Sort orders of the seller's offer list.</summary>
public enum SellerOfferSort
{
    Newest,
    Name,
    PriceAsc,
    PriceDesc,

    /// <summary>Lowest available first (what to restock next).</summary>
    Available
}

/// <summary>Filters of the seller's offer list (all combined with AND).</summary>
public sealed record SellerOfferFilter
{
    /// <summary>Matched against the product name and model number.</summary>
    public string? Keyword { get; init; }

    /// <summary>"New" or "Used"; null = both.</summary>
    public string? Condition { get; init; }

    public OfferStockFilter Stock { get; init; } = OfferStockFilter.All;
}

/// <summary>One offer of the signed-in seller, with the product data the list shows.</summary>
public sealed record SellerOfferRow(
    int Id,
    int ProductId,
    string ProductName,
    string ProductSlug,
    string ProductModelNumber,
    string? ImageUrl,
    bool ProductIsPublished,
    string Condition,
    decimal Price,
    int Stock,
    int ReservedStock,
    int LowStockThreshold,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
