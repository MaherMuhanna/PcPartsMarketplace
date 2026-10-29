using System.Globalization;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Mappings;

/// <summary>Manual entity-to-DTO mapping for categories, attributes and products.</summary>
public static class CatalogMappings
{
    public static CategorySummaryDto ToSummary(this Category category) =>
        new(category.Id, category.Name, category.Slug);

    public static CategoryDto ToDto(this Category category) =>
        new(category.Id, category.ParentId, category.Name, category.Slug, category.SortOrder);

    public static SpecOptionDto ToDto(this SpecOption option) =>
        new(option.Id, option.Value, option.Slug, option.SortOrder);

    public static SpecAttributeDto ToDto(this SpecAttribute attribute) =>
        new(attribute.Id,
            attribute.CategoryId,
            attribute.Name,
            attribute.Slug,
            attribute.DataType,
            attribute.Unit,
            attribute.IsFilterable,
            attribute.IsRequired,
            attribute.SortOrder,
            attribute.SpecOptions
                .Where(o => !o.IsDeleted)
                .OrderBy(o => o.SortOrder)
                .ThenBy(o => o.Value)
                .Select(o => o.ToDto())
                .ToList());

    public static BrandSummaryDto ToSummary(this Brand brand) =>
        new(brand.Id, brand.Name, brand.Slug);

    public static ProductListItemDto ToDto(this ProductListRow row) =>
        new(row.Id,
            row.Name,
            row.Slug,
            row.ModelNumber,
            new BrandSummaryDto(row.BrandId, row.BrandName, row.BrandSlug),
            new CategorySummaryDto(row.CategoryId, row.CategoryName, row.CategorySlug),
            row.MainImageUrl,
            row.MinPrice,
            row.OfferCount,
            row.InStock);

    public static ProductOfferDto ToDto(this ProductOfferRow row) =>
        new(row.OfferId, row.SellerId, row.StoreName, row.Condition, row.Price, row.Available);

    /// <summary>Spec values in the category's attribute order.</summary>
    public static IReadOnlyList<ProductSpecDto> ToSpecDtos(this Product product) =>
        product.ProductSpecValues
            .Where(v => v.SpecAttribute is not null)
            .OrderBy(v => v.SpecAttribute.SortOrder)
            .ThenBy(v => v.SpecAttribute.Name)
            .Select(v => new ProductSpecDto(
                v.SpecAttributeId,
                v.SpecAttribute.Name,
                v.SpecAttribute.Slug,
                v.SpecAttribute.DataType,
                v.SpecAttribute.Unit,
                v.TextValue,
                v.NumberValue,
                v.SpecOptionId,
                DisplayValue(v)))
            .ToList();

    public static IReadOnlyList<ProductImageDto> ToImageDtos(this Product product) =>
        product.ProductImages
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .Select(i => new ProductImageDto(i.Url, i.AltText, i.SortOrder))
            .ToList();

    public static ProductManageDto ToManageDto(this Product product) =>
        new(product.Id,
            product.Name,
            product.Slug,
            product.ModelNumber,
            product.Description,
            product.CategoryId,
            product.Category.Name,
            product.BrandId,
            product.Brand.Name,
            product.Status,
            product.ReviewNote,
            product.IsActive,
            product.ProposedBySellerId,
            product.ProposedBySeller?.StoreName,
            product.ToSpecDtos(),
            product.ToImageDtos(),
            product.CreatedAt,
            product.UpdatedAt);

    public static ProductManageListItemDto ToManageListItemDto(this Product product) =>
        new(product.Id,
            product.Name,
            product.Slug,
            product.ModelNumber,
            product.Category.Name,
            product.Brand.Name,
            product.Status,
            product.ReviewNote,
            product.IsActive,
            product.ProposedBySeller?.StoreName,
            product.CreatedAt);

    /// <summary>"16 GB", "4.2 GHz", "DDR5", or the free text.</summary>
    public static string DisplayValue(ProductSpecValue value)
    {
        if (value.SpecOption is not null)
        {
            return value.SpecOption.Value;
        }

        if (value.NumberValue is { } number)
        {
            var text = FormatNumber(number);
            return value.SpecAttribute.Unit is { Length: > 0 } unit ? $"{text} {unit}" : text;
        }

        return value.TextValue ?? string.Empty;
    }

    /// <summary>Invariant culture, no trailing zeros: 4.2000 -> "4.2", 16.0000 -> "16".</summary>
    public static string FormatNumber(decimal number) =>
        number.ToString("0.####", CultureInfo.InvariantCulture);

    public static bool IsDataType(this SpecAttribute attribute, SpecDataType type) =>
        string.Equals(attribute.DataType, type.ToString(), StringComparison.Ordinal);
}
