using System.ComponentModel.DataAnnotations;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.Validators;

namespace PCM.UI.Models;

/// <summary>"My offers" list: stock tabs, filters, sort and paging (all kept in the URL).</summary>
public sealed class SellerOffersViewModel
{
    /// <summary>Stock tabs in display order: query value (null = all) and label.</summary>
    public static readonly (string? Value, string Label)[] StockTabs =
        [(null, "All"), ("in", "In stock"), ("low", "Low stock"), ("out", "Out of stock")];

    public static readonly (string Value, string Label)[] SortOptions =
        [("newest", "Newest"), ("name", "Product name"), ("price", "Price: low to high"), ("-price", "Price: high to low"), ("available", "Available: lowest first")];

    public PagedResponse<OfferDto> Offers { get; init; } = new([], 1, 20, 0, 0);

    public string? Q { get; init; }

    public string? Condition { get; init; }

    public string? Stock { get; init; }

    public string? Sort { get; init; }
}

/// <summary>Step 1 of "Add offer": find the catalog product to sell.</summary>
public sealed class OfferProductPickerViewModel
{
    public string? Q { get; init; }

    /// <summary>Null until the seller searches.</summary>
    public PagedResponse<ProductListItemDto>? Results { get; init; }
}

/// <summary>Step 2 of "Add offer": condition, price and initial stock for the chosen product.</summary>
public sealed class OfferCreateViewModel
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    /// <summary>Product shown above the form (reloaded from the API, never posted back).</summary>
    public ProductDetailsDto? Product { get; set; }

    [Required, RegularExpression("^(New|Used)$", ErrorMessage = OfferRules.ConditionMessage)]
    public string Condition { get; set; } = "New";

    [Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Price must be between 0.01 and 999,999.99.")]
    [Display(Name = "Price (JOD)")]
    public decimal Price { get; set; }

    [Range(0, OfferRules.MaxQuantity)]
    [Display(Name = "Initial stock")]
    public int Stock { get; set; }

    [Range(0, OfferRules.MaxLowStockThreshold)]
    [Display(Name = "Low-stock alert at")]
    public int LowStockThreshold { get; set; } = OfferRules.DefaultLowStockThreshold;
}

/// <summary>Edit price and low-stock threshold. Product, condition and stock are shown read-only.</summary>
public sealed class OfferEditViewModel
{
    public int Id { get; set; }

    /// <summary>Current offer shown above the form (reloaded from the API, never posted back).</summary>
    public OfferDto? Offer { get; set; }

    [Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Price must be between 0.01 and 999,999.99.")]
    [Display(Name = "Price (JOD)")]
    public decimal Price { get; set; }

    [Range(0, OfferRules.MaxLowStockThreshold)]
    [Display(Name = "Low-stock alert at")]
    public int LowStockThreshold { get; set; }

    public static OfferEditViewModel From(OfferDto offer) => new()
    {
        Id = offer.Id,
        Offer = offer,
        Price = offer.Price,
        LowStockThreshold = offer.LowStockThreshold
    };
}

/// <summary>One offer with its inventory history.</summary>
public sealed class OfferHistoryViewModel
{
    public required OfferDto Offer { get; init; }

    public required PagedResponse<StockMovementDto> Movements { get; init; }
}
