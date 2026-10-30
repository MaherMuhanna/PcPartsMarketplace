using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Repositories.Common;
using PCM.Repositories.IRepository;
using PCM.Repositories.Models;

namespace PCM.Repositories.Repository;

public class ProductRepository(PcPartsContext context) : Repository<Product>(context), IProductRepository
{
    // Compile-time constants, so EF inlines them as SQL literals.
    private const string Approved = nameof(ProductStatus.Approved);
    private const string ApprovedSeller = nameof(SellerStatus.Approved);

    /// <summary>Products visible in the public catalog.</summary>
    private IQueryable<Product> Published =>
        DbSet.AsNoTracking().Where(p => p.Status == Approved && p.IsActive);

    // ---------------------------------------------------------------------
    // Public catalog
    // ---------------------------------------------------------------------

    public async Task<PagedResult<ProductListRow>> SearchAsync(
        ProductSearchCriteria criteria, int page, int pageSize, ProductSort sort, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = ApplyFilters(Published, criteria);

        // "Min price" = cheapest offer an approved seller has in stock. Products without
        // offers have no price; they never match a price filter and sort after priced ones.
        // The expression is written inline (not shared) because EF must translate it to SQL.
        if (criteria.MinPrice is { } minPrice)
        {
            query = query.Where(p => p.Offers
                .Where(o => o.Seller.Status == ApprovedSeller && o.Stock - o.ReservedStock > 0)
                .Min(o => (decimal?)o.Price) >= minPrice);
        }

        if (criteria.MaxPrice is { } maxPrice)
        {
            query = query.Where(p => p.Offers
                .Where(o => o.Seller.Status == ApprovedSeller && o.Stock - o.ReservedStock > 0)
                .Min(o => (decimal?)o.Price) <= maxPrice);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Id is the tie-breaker so paging is stable when sort keys are equal.
        var ordered = sort switch
        {
            ProductSort.Name => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ProductSort.PriceAsc => query
                .OrderBy(p => p.Offers.Where(o => o.Seller.Status == ApprovedSeller && o.Stock - o.ReservedStock > 0).Min(o => (decimal?)o.Price) == null)
                .ThenBy(p => p.Offers.Where(o => o.Seller.Status == ApprovedSeller && o.Stock - o.ReservedStock > 0).Min(o => (decimal?)o.Price))
                .ThenBy(p => p.Id),
            ProductSort.PriceDesc => query
                .OrderBy(p => p.Offers.Where(o => o.Seller.Status == ApprovedSeller && o.Stock - o.ReservedStock > 0).Min(o => (decimal?)o.Price) == null)
                .ThenByDescending(p => p.Offers.Where(o => o.Seller.Status == ApprovedSeller && o.Stock - o.ReservedStock > 0).Min(o => (decimal?)o.Price))
                .ThenBy(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListRow(
                p.Id,
                p.Name,
                p.Slug,
                p.ModelNumber,
                p.BrandId,
                p.Brand.Name,
                p.Brand.Slug,
                p.CategoryId,
                p.Category.Name,
                p.Category.Slug,
                p.ProductImages.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Url).FirstOrDefault(),
                p.Offers.Where(o => o.Seller.Status == ApprovedSeller && o.Stock - o.ReservedStock > 0).Min(o => (decimal?)o.Price),
                p.Offers.Count(o => o.Seller.Status == ApprovedSeller),
                p.Offers.Any(o => o.Seller.Status == ApprovedSeller && o.Stock - o.ReservedStock > 0)))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListRow>(items, page, pageSize, totalCount);
    }

    public Task<Product?> GetPublishedByIdAsync(int id, CancellationToken cancellationToken = default) =>
        WithDetails(Published).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Product?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        WithDetails(Published).FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);

    public async Task<IReadOnlyList<ProductOfferRow>> GetVisibleOffersAsync(int productId, CancellationToken cancellationToken = default) =>
        await Context.Offers.AsNoTracking()
            .Where(o => o.ProductId == productId && o.Seller.Status == ApprovedSeller)
            .OrderBy(o => o.Price)
            .ThenBy(o => o.Id)
            .Select(o => new ProductOfferRow(o.Id, o.SellerId, o.Seller.StoreName, o.Condition, o.Price, o.Stock - o.ReservedStock))
            .ToListAsync(cancellationToken);

    public Task<bool> IsPublishedAsync(int id, CancellationToken cancellationToken = default) =>
        Published.AnyAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<string, int>> GetPublishedIdsBySlugsAsync(
        IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default) =>
        await Published
            .Where(p => slugs.Contains(p.Slug))
            .ToDictionaryAsync(p => p.Slug, p => p.Id, cancellationToken);

    public async Task<IReadOnlyList<FacetCount>> GetBrandFacetsAsync(IReadOnlyCollection<int>? categoryIds, CancellationToken cancellationToken = default)
    {
        var query = Published;
        if (categoryIds is not null)
        {
            query = query.Where(p => categoryIds.Contains(p.CategoryId));
        }

        return await query
            .GroupBy(p => p.BrandId)
            .Select(g => new FacetCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FacetCount>> GetOptionFacetsAsync(int categoryId, CancellationToken cancellationToken = default) =>
        await PublishedSpecValues(categoryId)
            .Where(v => v.SpecOptionId != null)
            .GroupBy(v => v.SpecOptionId!.Value)
            .Select(g => new FacetCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NumberFacet>> GetNumberFacetsAsync(int categoryId, CancellationToken cancellationToken = default) =>
        await PublishedSpecValues(categoryId)
            .Where(v => v.NumberValue != null)
            .GroupBy(v => new { v.SpecAttributeId, Value = v.NumberValue!.Value })
            .Select(g => new NumberFacet(g.Key.SpecAttributeId, g.Key.Value, g.Count()))
            .ToListAsync(cancellationToken);

    // ---------------------------------------------------------------------
    // Admin and seller management
    // ---------------------------------------------------------------------

    public Task<Product?> GetForEditAsync(int id, CancellationToken cancellationToken = default) =>
        DbSet
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProposedBySeller)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductSpecValues).ThenInclude(v => v.SpecAttribute)
            .Include(p => p.ProductSpecValues).ThenInclude(v => v.SpecOption)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<PagedResult<Product>> GetManagePagedAsync(
        ProductManageFilter filter, int page, int pageSize, ProductManageSort sort, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = DbSet.AsNoTracking()
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProposedBySeller)
            .AsQueryable();

        if (filter.Status is not null)
        {
            query = query.Where(p => p.Status == filter.Status);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (filter.ProposedBySellerId is { } sellerId)
        {
            query = query.Where(p => p.ProposedBySellerId == sellerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(p => p.Name.Contains(keyword) || p.ModelNumber.Contains(keyword));
        }

        var ordered = sort switch
        {
            ProductManageSort.Oldest => query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            ProductManageSort.Name => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        return ordered.ToPagedResultAsync(page, pageSize, cancellationToken);
    }

    // ---------------------------------------------------------------------

    /// <summary>Category, brand, keyword and spec filters (everything except price).</summary>
    private static IQueryable<Product> ApplyFilters(IQueryable<Product> query, ProductSearchCriteria criteria)
    {
        if (criteria.CategoryIds is { } categoryIds)
        {
            query = query.Where(p => categoryIds.Contains(p.CategoryId));
        }

        if (criteria.BrandIds is { Count: > 0 } brandIds)
        {
            query = query.Where(p => brandIds.Contains(p.BrandId));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Keyword))
        {
            // EF Core translates Contains to LIKE with the wildcards escaped.
            var keyword = criteria.Keyword.Trim();
            query = query.Where(p => p.Name.Contains(keyword) || p.ModelNumber.Contains(keyword));
        }

        // One EXISTS per attribute: AND between attributes, OR between the options of one attribute.
        foreach (var filter in criteria.OptionFilters)
        {
            var attributeId = filter.AttributeId;
            var optionIds = filter.OptionIds;
            query = query.Where(p => p.ProductSpecValues.Any(v =>
                v.SpecAttributeId == attributeId && v.SpecOptionId != null && optionIds.Contains(v.SpecOptionId.Value)));
        }

        foreach (var filter in criteria.NumberFilters)
        {
            var attributeId = filter.AttributeId;
            if (filter.Values is { Count: > 0 } values)
            {
                // Picked values (OR), e.g. Capacity 32 or 64.
                query = query.Where(p => p.ProductSpecValues.Any(v =>
                    v.SpecAttributeId == attributeId && v.NumberValue != null && values.Contains(v.NumberValue.Value)));
            }
            else if (filter.Min is { } min && filter.Max is { } max)
            {
                query = query.Where(p => p.ProductSpecValues.Any(v =>
                    v.SpecAttributeId == attributeId && v.NumberValue >= min && v.NumberValue <= max));
            }
            else if (filter.Min is { } onlyMin)
            {
                query = query.Where(p => p.ProductSpecValues.Any(v =>
                    v.SpecAttributeId == attributeId && v.NumberValue >= onlyMin));
            }
            else if (filter.Max is { } onlyMax)
            {
                query = query.Where(p => p.ProductSpecValues.Any(v =>
                    v.SpecAttributeId == attributeId && v.NumberValue <= onlyMax));
            }
        }

        return query;
    }

    /// <summary>Spec values of the published products of one category.</summary>
    private IQueryable<ProductSpecValue> PublishedSpecValues(int categoryId) =>
        Context.ProductSpecValues.AsNoTracking()
            .Where(v => v.Product.CategoryId == categoryId && v.Product.Status == Approved && v.Product.IsActive);

    private static IQueryable<Product> WithDetails(IQueryable<Product> query) =>
        query
            .Include(p => p.Brand)
            .Include(p => p.Category)
            .Include(p => p.ProductImages)
            .Include(p => p.ProductSpecValues).ThenInclude(v => v.SpecAttribute)
            .Include(p => p.ProductSpecValues).ThenInclude(v => v.SpecOption)
            .AsSplitQuery();
}
