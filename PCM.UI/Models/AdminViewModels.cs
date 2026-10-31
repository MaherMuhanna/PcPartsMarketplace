using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.UI.Models;

/// <summary>Admin > Orders list: status tabs, order-number search, sort and paging (all kept in the URL).</summary>
public sealed class AdminOrdersViewModel
{
    /// <summary>Status tabs, in display order (null = all). An order is listed if any of its shipments has the status.</summary>
    public static readonly string?[] StatusTabs = [null, "Pending", "Confirmed", "Shipped", "Delivered", "Cancelled"];

    public PagedResponse<OrderListItemDto> Orders { get; init; } = new([], 1, 20, 0, 0);

    public string? Status { get; init; }

    public string? Q { get; init; }

    /// <summary>Null = newest first; "oldest" = oldest first.</summary>
    public string? Sort { get; init; }
}

/// <summary>Admin > Brands list.</summary>
public sealed class AdminBrandsViewModel
{
    public PagedResponse<PCM.Infrastructure.DTO.Brands.BrandDto> Brands { get; init; } = new([], 1, 50, 0, 0);
}

/// <summary>Admin > Categories and specs: the tree, the selected category and (for a leaf) its attributes.</summary>
public sealed class AdminCategoriesViewModel
{
    public static readonly string[] DataTypes = ["Text", "Number", "Enum"];

    public IReadOnlyList<PCM.Infrastructure.DTO.Catalog.CategoryTreeDto> Tree { get; init; } = [];

    public CategoryEntry? Selected { get; init; }

    public IReadOnlyList<PCM.Infrastructure.DTO.Catalog.SpecAttributeDto> Attributes { get; init; } = [];
}

/// <summary>Posted by the add and edit attribute forms. Rules (Text cannot be filterable, type locked once used) are the API's.</summary>
public sealed class AttributeFormModel
{
    public string? Name { get; set; }

    public string? DataType { get; set; }

    public string? Unit { get; set; }

    public bool IsFilterable { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public PCM.Infrastructure.DTO.Catalog.SpecAttributeRequest ToRequest() => new()
    {
        Name = Name?.Trim() ?? string.Empty,
        DataType = DataType?.Trim() ?? string.Empty,
        Unit = string.IsNullOrWhiteSpace(Unit) ? null : Unit.Trim(),
        IsFilterable = IsFilterable,
        IsRequired = IsRequired,
        SortOrder = SortOrder
    };
}

/// <summary>Input of the recursive _CategoryTreeNodes partial.</summary>
public sealed record CategoryTreeNodesModel(IReadOnlyList<PCM.Infrastructure.DTO.Catalog.CategoryTreeDto> Nodes, int? SelectedId);

/// <summary>Input of the _AttributeFields partial: the attribute being edited, or null for the add form.</summary>
public sealed record AttributeFieldsModel(PCM.Infrastructure.DTO.Catalog.SpecAttributeDto? Attribute);

/// <summary>Admin > Products list (status tabs; Pending = review queue), with filters kept in the URL.</summary>
public sealed class AdminProductsViewModel
{
    /// <summary>Status tabs in display order (null = all).</summary>
    public static readonly string?[] StatusTabs = [null, "Pending", "Approved", "Rejected"];

    public static readonly (string? Value, string Label)[] SortOptions = [(null, "Newest"), ("oldest", "Oldest"), ("name", "Name")];

    public PagedResponse<PCM.Infrastructure.DTO.Catalog.ProductManageListItemDto> Products { get; init; } = new([], 1, 20, 0, 0);

    public string? Status { get; init; }

    public int? CategoryId { get; init; }

    public string? Q { get; init; }

    public string? Sort { get; init; }

    public IReadOnlyList<CategoryEntry> Categories { get; init; } = [];
}
