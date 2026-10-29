namespace PCM.Infrastructure.DTO.Catalog;

/// <summary>Short reference to a category (breadcrumbs, product cards).</summary>
public sealed record CategorySummaryDto(int Id, string Name, string Slug);

/// <summary>Node of GET /api/v1/categories. Products attach to leaves only.</summary>
public sealed record CategoryTreeDto(
    int Id,
    string Name,
    string Slug,
    int SortOrder,
    bool IsLeaf,
    IReadOnlyList<CategoryTreeDto> Children);

/// <summary>A category as returned by the admin endpoints.</summary>
public sealed record CategoryDto(int Id, int? ParentId, string Name, string Slug, int SortOrder);

/// <summary>POST /api/v1/admin/categories. ParentId null creates a top-level category.</summary>
public sealed record CreateCategoryRequest
{
    public int? ParentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

/// <summary>PUT /api/v1/admin/categories/{id}. Renaming regenerates the slug; the parent cannot change.</summary>
public sealed record UpdateCategoryRequest
{
    public string Name { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

/// <summary>An attribute a leaf category defines, with its allowed options (Enum only).</summary>
public sealed record SpecAttributeDto(
    int Id,
    int CategoryId,
    string Name,
    string Slug,
    string DataType,
    string? Unit,
    bool IsFilterable,
    bool IsRequired,
    int SortOrder,
    IReadOnlyList<SpecOptionDto> Options);

public sealed record SpecOptionDto(int Id, string Value, string Slug, int SortOrder);

/// <summary>POST /api/v1/admin/categories/{id}/attributes and PUT .../attributes/{attributeId}.</summary>
public sealed record SpecAttributeRequest
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Text, Number or Enum.</summary>
    public string DataType { get; init; } = string.Empty;

    public string? Unit { get; init; }

    /// <summary>Shown as a catalog filter. Only Number and Enum attributes can be filterable.</summary>
    public bool IsFilterable { get; init; }

    /// <summary>Every product of the category must have a value.</summary>
    public bool IsRequired { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>POST /api/v1/admin/attributes/{id}/options and PUT .../options/{optionId}.</summary>
public sealed record SpecOptionRequest
{
    public string Value { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

/// <summary>
/// GET /api/v1/products/filters: what the catalog sidebar can offer for the current category.
/// Spec attributes are listed only for a leaf category.
/// </summary>
public sealed record CatalogFiltersDto(
    CategorySummaryDto? Category,
    bool IsLeaf,
    IReadOnlyList<CategorySummaryDto> Path,
    IReadOnlyList<CategorySummaryDto> Children,
    IReadOnlyList<BrandFacetDto> Brands,
    IReadOnlyList<AttributeFilterDto> Attributes);

/// <summary>A brand with the number of published products it has in the current scope.</summary>
public sealed record BrandFacetDto(string Slug, string Name, int Count);

/// <summary>
/// A filterable attribute with its choices and product counts. Enum: the attribute's options.
/// Number: the distinct values products have (slug "32", value "32 GB"), plus <see cref="Min"/> and <see cref="Max"/>.
/// </summary>
public sealed record AttributeFilterDto(
    string Slug,
    string Name,
    string DataType,
    string? Unit,
    IReadOnlyList<OptionFacetDto> Options,
    decimal? Min,
    decimal? Max);

public sealed record OptionFacetDto(string Slug, string Value, int Count);
