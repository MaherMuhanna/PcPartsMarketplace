using System.Globalization;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Catalog;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Services;

public sealed class ProductCatalogService(IUnitOfWork unitOfWork) : IProductCatalogService
{
    public async Task<PagedResponse<ProductListItemDto>> SearchAsync(
        ProductListQuery query,
        IReadOnlyDictionary<string, string> specFilters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(specFilters);

        var tree = new CategoryTree(await unitOfWork.Categories.GetAllAsync(cancellationToken));
        var category = FindCategory(tree, query.Category);

        // Brands: unknown slugs are ignored, but if none of the requested brands exists
        // the answer is "nothing", not "every brand".
        IReadOnlyCollection<int>? brandIds = null;
        var brandSlugs = SplitValues(query.Brand ?? []);
        if (brandSlugs.Count > 0)
        {
            brandIds = (await unitOfWork.Brands.GetBySlugsAsync(brandSlugs, cancellationToken)).Select(b => b.Id).ToList();
            if (brandIds.Count == 0)
            {
                return new PagedResponse<ProductListItemDto>([], query.Page, query.PageSize, 0, 0);
            }
        }

        // Spec filters only make sense inside one leaf category (attributes are defined there).
        var optionFilters = new List<OptionFilter>();
        var numberFilters = new List<NumberFilter>();
        if (category is not null && tree.IsLeaf(category.Id) && specFilters.Count > 0)
        {
            var attributes = await unitOfWork.SpecAttributes.GetByCategoryAsync(category.Id, cancellationToken);
            BuildSpecFilters(attributes, specFilters, optionFilters, numberFilters);
        }

        var criteria = new ProductSearchCriteria
        {
            CategoryIds = category is null ? null : tree.SelfAndDescendantIds(category.Id),
            BrandIds = brandIds,
            Keyword = string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim(),
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            OptionFilters = optionFilters,
            NumberFilters = numberFilters
        };

        var page = await unitOfWork.Products.SearchAsync(criteria, query.Page, query.PageSize, ParseSort(query.Sort), cancellationToken);
        return page.ToResponse(row => row.ToDto());
    }

    public async Task<CatalogFiltersDto> GetFiltersAsync(string? categorySlug, CancellationToken cancellationToken = default)
    {
        var tree = new CategoryTree(await unitOfWork.Categories.GetAllAsync(cancellationToken));
        var category = FindCategory(tree, categorySlug);

        var scope = category is null ? null : tree.SelfAndDescendantIds(category.Id);
        var brands = await BrandFacetsAsync(scope, cancellationToken);

        if (category is null)
        {
            return new CatalogFiltersDto(
                Category: null,
                IsLeaf: false,
                Path: [],
                Children: tree.Roots.Select(c => c.ToSummary()).ToList(),
                Brands: brands,
                Attributes: []);
        }

        var isLeaf = tree.IsLeaf(category.Id);
        IReadOnlyList<AttributeFilterDto> attributes = isLeaf ? await AttributeFiltersAsync(category.Id, cancellationToken) : [];

        return new CatalogFiltersDto(
            category.ToSummary(),
            isLeaf,
            tree.PathTo(category.Id).Select(c => c.ToSummary()).ToList(),
            tree.ChildrenOf(category.Id).Select(c => c.ToSummary()).ToList(),
            brands,
            attributes);
    }

    public async Task<ProductDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await unitOfWork.Products.GetPublishedByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product {id} was not found.");

        return await ToDetailsAsync(product, cancellationToken);
    }

    public async Task<ProductDetailsDto> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var product = await unitOfWork.Products.GetPublishedBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new NotFoundException($"Product '{slug}' was not found.");

        return await ToDetailsAsync(product, cancellationToken);
    }

    // ---------------------------------------------------------------------

    private static Category? FindCategory(CategoryTree tree, string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        return tree.FindBySlug(slug) ?? throw new NotFoundException($"Category '{slug}' was not found.");
    }

    /// <summary>
    /// Turns raw spec filters into repository filters. Keys and values are matched
    /// case-insensitively; keys that are not filterable attributes of the category are ignored.
    /// </summary>
    internal static void BuildSpecFilters(
        IReadOnlyList<SpecAttribute> attributes,
        IReadOnlyDictionary<string, string> specFilters,
        List<OptionFilter> optionFilters,
        List<NumberFilter> numberFilters)
    {
        var bySlug = attributes.Where(a => a.IsFilterable).ToDictionary(a => a.Slug, StringComparer.OrdinalIgnoreCase);
        var errors = new Dictionary<string, string[]>();

        foreach (var (key, raw) in specFilters)
        {
            if (!bySlug.TryGetValue(key, out var attribute) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (attribute.IsDataType(SpecDataType.Enum))
            {
                // Unknown option slugs match nothing; if none is known, the filter matches no product.
                var wanted = SplitValues([raw]);
                var optionIds = attribute.SpecOptions
                    .Where(o => wanted.Contains(o.Slug, StringComparer.OrdinalIgnoreCase))
                    .Select(o => o.Id)
                    .ToList();
                optionFilters.Add(new OptionFilter(attribute.Id, optionIds));
            }
            else if (attribute.IsDataType(SpecDataType.Number))
            {
                if (TryParseRange(raw, out var min, out var max))
                {
                    numberFilters.Add(new NumberFilter(attribute.Id, min, max));
                }
                else
                {
                    errors[attribute.Slug] = [$"Use min-max, min-, -max or one number, e.g. {attribute.Slug}=16-64."];
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    /// <summary>
    /// Parses "16-64", "16-", "-64" or "32" (exactly 32). Numbers use '.' as the decimal separator
    /// and must not be negative. False if the text does not match, or min is greater than max.
    /// </summary>
    public static bool TryParseRange(string text, out decimal? min, out decimal? max)
    {
        min = null;
        max = null;
        var parts = text.Trim().Split('-');

        if (parts.Length == 1)
        {
            if (!TryParseNumber(parts[0], out var exact))
            {
                return false;
            }

            min = max = exact;
            return true;
        }

        if (parts.Length != 2 || (parts[0].Length == 0 && parts[1].Length == 0))
        {
            return false;
        }

        if (parts[0].Length > 0)
        {
            if (!TryParseNumber(parts[0], out var from))
            {
                return false;
            }

            min = from;
        }

        if (parts[1].Length > 0)
        {
            if (!TryParseNumber(parts[1], out var to))
            {
                return false;
            }

            max = to;
        }

        return min is null || max is null || min <= max;
    }

    private static bool TryParseNumber(string text, out decimal value) =>
        decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);

    /// <summary>Splits "a,b" values, trims, lower-cases and removes blanks and duplicates.</summary>
    private static List<string> SplitValues(IEnumerable<string> values) =>
        values
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(v => v.ToLowerInvariant())
            .Distinct()
            .ToList();

    private async Task<IReadOnlyList<BrandFacetDto>> BrandFacetsAsync(IReadOnlyCollection<int>? categoryIds, CancellationToken cancellationToken)
    {
        var counts = await unitOfWork.Products.GetBrandFacetsAsync(categoryIds, cancellationToken);
        if (counts.Count == 0)
        {
            return [];
        }

        var countById = counts.ToDictionary(c => c.Id, c => c.Count);
        var brands = await unitOfWork.Brands.GetByIdsAsync(countById.Keys, cancellationToken);
        return brands.Select(b => new BrandFacetDto(b.Slug, b.Name, countById[b.Id])).ToList();
    }

    private async Task<IReadOnlyList<AttributeFilterDto>> AttributeFiltersAsync(int categoryId, CancellationToken cancellationToken)
    {
        var attributes = await unitOfWork.SpecAttributes.GetByCategoryAsync(categoryId, cancellationToken);
        var optionCounts = (await unitOfWork.Products.GetOptionFacetsAsync(categoryId, cancellationToken))
            .ToDictionary(f => f.Id, f => f.Count);
        var ranges = (await unitOfWork.Products.GetNumberRangesAsync(categoryId, cancellationToken))
            .ToDictionary(r => r.AttributeId);

        var result = new List<AttributeFilterDto>();
        foreach (var attribute in attributes.Where(a => a.IsFilterable))
        {
            if (attribute.IsDataType(SpecDataType.Enum))
            {
                var options = attribute.SpecOptions
                    .Select(o => new OptionFacetDto(o.Slug, o.Value, optionCounts.GetValueOrDefault(o.Id)))
                    .ToList();
                result.Add(new AttributeFilterDto(attribute.Slug, attribute.Name, attribute.DataType, attribute.Unit, options, null, null));
            }
            else if (attribute.IsDataType(SpecDataType.Number) && ranges.TryGetValue(attribute.Id, out var range))
            {
                // A number attribute no product has a value for is not worth a filter.
                result.Add(new AttributeFilterDto(attribute.Slug, attribute.Name, attribute.DataType, attribute.Unit, [], range.Min, range.Max));
            }
        }

        return result;
    }

    private async Task<ProductDetailsDto> ToDetailsAsync(Product product, CancellationToken cancellationToken)
    {
        var tree = new CategoryTree(await unitOfWork.Categories.GetAllAsync(cancellationToken));
        var offers = await unitOfWork.Products.GetVisibleOffersAsync(product.Id, cancellationToken);

        return new ProductDetailsDto(
            product.Id,
            product.Name,
            product.Slug,
            product.ModelNumber,
            product.Description,
            product.Brand.ToSummary(),
            tree.PathTo(product.CategoryId).Select(c => c.ToSummary()).ToList(),
            product.ToSpecDtos(),
            product.ToImageDtos(),
            offers.Select(o => o.ToDto()).ToList());
    }

    private static ProductSort ParseSort(string? sort) => sort?.ToLowerInvariant() switch
    {
        "name" => ProductSort.Name,
        "price" => ProductSort.PriceAsc,
        "-price" => ProductSort.PriceDesc,
        _ => ProductSort.Newest
    };
}
