using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.DTO.Offers;

/// <summary>
/// One offer of the signed-in seller. Available = Stock - ReservedStock (what customers can still buy);
/// IsLowStock = Available &lt;= LowStockThreshold. ProductIsPublished is false when an admin
/// deactivated the product: the offer stays here but is hidden from the catalog.
/// </summary>
public sealed record OfferDto(
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
    int Available,
    int LowStockThreshold,
    bool IsLowStock,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>One row of an offer's inventory history. Change is signed (+ added, - removed).</summary>
public sealed record StockMovementDto(
    int Id,
    string Reason,
    int Change,
    int StockAfter,
    int ReservedStockAfter,
    string? Note,
    int? SellerOrderId,
    DateTime CreatedAt);

/// <summary>
/// GET /api/v1/seller/offers. Stock: all (default), in, low, out. Condition: New / Used.
/// Sort: newest (default), name, price, -price, available (lowest available first).
/// </summary>
public sealed class SellerOfferListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest", "name", "price", "-price", "available"];

    public static readonly string[] StockFilters = ["all", "in", "low", "out"];

    /// <summary>Keyword matched against the product name and model number.</summary>
    public string? Q { get; init; }

    public string? Condition { get; init; }

    public string? Stock { get; init; }
}

/// <summary>GET /api/v1/seller/offers/{id}/movements (newest first; the only sort is "newest").</summary>
public sealed class StockMovementListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest"];
}

/// <summary>Body of POST /api/v1/seller/offers.</summary>
public sealed record CreateOfferRequest
{
    /// <summary>A published product (Approved and active).</summary>
    public int ProductId { get; init; }

    /// <summary>New or Used. One offer per product and condition.</summary>
    public string Condition { get; init; } = "New";

    /// <summary>Price in JOD, 0.01 to 999,999.99, at most 2 decimals.</summary>
    public decimal Price { get; init; }

    /// <summary>Initial stock, 0 to 100,000. Recorded as a Restock movement when above 0.</summary>
    public int Stock { get; init; }

    /// <summary>Warn when available stock drops to this level (0 to 1,000). Default 5.</summary>
    public int? LowStockThreshold { get; init; }
}

/// <summary>
/// Body of PUT /api/v1/seller/offers/{id}. Product and condition are fixed; stock changes
/// only through PATCH .../stock so every change is recorded.
/// </summary>
public sealed record UpdateOfferRequest
{
    public decimal Price { get; init; }

    public int LowStockThreshold { get; init; }
}

/// <summary>
/// Body of PATCH /api/v1/seller/offers/{id}/stock.
/// Restock: Change &gt; 0 (goods received), note optional.
/// Adjustment: Change positive or negative (stock count, damage, returns), note required.
/// </summary>
public sealed record StockChangeRequest
{
    /// <summary>Restock or Adjustment.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Signed quantity, non-zero, at most 100,000 either way.</summary>
    public int Change { get; init; }

    public string? Note { get; init; }
}
