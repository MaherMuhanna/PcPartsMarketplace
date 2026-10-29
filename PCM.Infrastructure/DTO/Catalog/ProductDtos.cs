using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.DTO.Catalog;

// ---------------------------------------------------------------------
// Public catalog
// ---------------------------------------------------------------------

/// <summary>Short reference to a brand.</summary>
public sealed record BrandSummaryDto(int Id, string Name, string Slug);

/// <summary>
/// GET /api/v1/products query (the known keys). Spec filters are extra keys named after
/// attribute slugs, read by the controller, e.g. ?memory-type=ddr4,ddr5&amp;capacity=16-64.
/// Sort: newest (default), name, price, -price.
/// </summary>
public sealed class ProductListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest", "name", "price", "-price"];

    /// <summary>Keyword matched against product name and model number.</summary>
    public string? Q { get; init; }

    /// <summary>Category slug. A parent category includes the products of all its subcategories.</summary>
    public string? Category { get; init; }

    /// <summary>Brand slugs: ?brand=amd,intel or ?brand=amd&amp;brand=intel (OR).</summary>
    public string[]? Brand { get; init; }

    /// <summary>Lowest offer price, inclusive.</summary>
    public decimal? MinPrice { get; init; }

    /// <summary>Highest offer price, inclusive.</summary>
    public decimal? MaxPrice { get; init; }
}

/// <summary>A product card in the catalog. Prices are null until a seller has the product in stock.</summary>
public sealed record ProductListItemDto(
    int Id,
    string Name,
    string Slug,
    string ModelNumber,
    BrandSummaryDto Brand,
    CategorySummaryDto Category,
    string? MainImageUrl,
    decimal? MinPrice,
    int OfferCount,
    bool InStock);

/// <summary>Product page: master product data plus the offers of approved sellers.</summary>
public sealed record ProductDetailsDto(
    int Id,
    string Name,
    string Slug,
    string ModelNumber,
    string? Description,
    BrandSummaryDto Brand,
    IReadOnlyList<CategorySummaryDto> CategoryPath,
    IReadOnlyList<ProductSpecDto> Specs,
    IReadOnlyList<ProductImageDto> Images,
    IReadOnlyList<ProductOfferDto> Offers);

/// <summary>
/// One spec value. <see cref="DisplayValue"/> is ready to show ("16 GB", "DDR5");
/// the raw fields are what an edit form sends back.
/// </summary>
public sealed record ProductSpecDto(
    int AttributeId,
    string Name,
    string Slug,
    string DataType,
    string? Unit,
    string? TextValue,
    decimal? NumberValue,
    int? OptionId,
    string DisplayValue);

public sealed record ProductImageDto(string Url, string? AltText, int SortOrder);

/// <summary>A seller's offer on the product page (offers arrive in Phase 4).</summary>
public sealed record ProductOfferDto(int OfferId, int SellerId, string StoreName, string Condition, decimal Price, int Available);

// ---------------------------------------------------------------------
// Admin and seller management
// ---------------------------------------------------------------------

/// <summary>
/// Body of POST/PUT /api/v1/admin/products and /api/v1/seller/products/proposals.
/// Specs and images are the complete new set: values not listed are removed.
/// </summary>
public sealed record ProductUpsertRequest
{
    /// <summary>Must be a leaf category.</summary>
    public int CategoryId { get; init; }

    public int BrandId { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Manufacturer part number; unique per brand.</summary>
    public string ModelNumber { get; init; } = string.Empty;

    public string? Description { get; init; }

    public List<SpecValueRequest> Specs { get; init; } = [];

    /// <summary>Up to 8 image URLs; the first one is the main image.</summary>
    public List<ProductImageRequest> Images { get; init; } = [];
}

/// <summary>
/// Value for one attribute of the category. Set exactly one field, matching the attribute type:
/// TextValue (Text), NumberValue (Number) or OptionId (Enum).
/// </summary>
public sealed record SpecValueRequest
{
    public int AttributeId { get; init; }

    public string? TextValue { get; init; }

    public decimal? NumberValue { get; init; }

    public int? OptionId { get; init; }
}

public sealed record ProductImageRequest
{
    /// <summary>Absolute http(s) URL, or a site path such as /images/products/cpu.svg.</summary>
    public string Url { get; init; } = string.Empty;

    public string? AltText { get; init; }
}

/// <summary>Body of POST /api/v1/admin/products/{id}/reject.</summary>
public sealed record ProductReviewRequest
{
    /// <summary>Why the proposal was rejected; shown to the seller.</summary>
    public string Note { get; init; } = string.Empty;
}

/// <summary>A product as admins and proposing sellers see it: any status, with review details.</summary>
public sealed record ProductManageDto(
    int Id,
    string Name,
    string Slug,
    string ModelNumber,
    string? Description,
    int CategoryId,
    string CategoryName,
    int BrandId,
    string BrandName,
    string Status,
    string? ReviewNote,
    bool IsActive,
    int? ProposedBySellerId,
    string? ProposedByStoreName,
    IReadOnlyList<ProductSpecDto> Specs,
    IReadOnlyList<ProductImageDto> Images,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Row of the admin and seller product lists.</summary>
public sealed record ProductManageListItemDto(
    int Id,
    string Name,
    string Slug,
    string ModelNumber,
    string CategoryName,
    string BrandName,
    string Status,
    string? ReviewNote,
    bool IsActive,
    string? ProposedByStoreName,
    DateTime CreatedAt);

/// <summary>GET /api/v1/admin/products. Status: Pending / Approved / Rejected. Sort: newest (default), oldest, name.</summary>
public sealed class AdminProductListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest", "oldest", "name"];

    public string? Status { get; init; }

    public int? CategoryId { get; init; }

    public string? Q { get; init; }
}

/// <summary>GET /api/v1/seller/products/proposals. Status: Pending / Approved / Rejected. Sort: newest (default), oldest, name.</summary>
public sealed class SellerProposalListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest", "oldest", "name"];

    public string? Status { get; init; }
}
