using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Repositories.Common;
using PCM.Repositories.IRepository;
using PCM.Repositories.Models;

namespace PCM.Repositories.Repository;

public class OfferRepository(PcPartsContext context) : Repository<Offer>(context), IOfferRepository
{
    // Compile-time constant, so EF inlines it as a SQL literal.
    private const string Approved = nameof(ProductStatus.Approved);
    private const string ApprovedSeller = nameof(SellerStatus.Approved);

    public async Task<PagedResult<SellerOfferRow>> GetSellerPagedAsync(
        int sellerId, SellerOfferFilter filter, int page, int pageSize, SellerOfferSort sort, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = ApplyFilters(DbSet.AsNoTracking().Where(o => o.SellerId == sellerId), filter);
        var totalCount = await query.CountAsync(cancellationToken);

        // Id is the tie-breaker so paging is stable when sort keys are equal.
        var ordered = sort switch
        {
            SellerOfferSort.Name => query.OrderBy(o => o.Product.Name).ThenBy(o => o.Id),
            SellerOfferSort.PriceAsc => query.OrderBy(o => o.Price).ThenBy(o => o.Id),
            SellerOfferSort.PriceDesc => query.OrderByDescending(o => o.Price).ThenBy(o => o.Id),
            SellerOfferSort.Available => query.OrderBy(o => o.Stock - o.ReservedStock).ThenBy(o => o.Id),
            _ => query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
        };

        var items = await ToRows(ordered.Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<SellerOfferRow>(items, page, pageSize, totalCount);
    }

    public Task<SellerOfferRow?> GetSellerRowAsync(int sellerId, int id, CancellationToken cancellationToken = default) =>
        ToRows(DbSet.AsNoTracking().Where(o => o.Id == id && o.SellerId == sellerId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Offer?> GetOwnedAsync(int sellerId, int id, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(o => o.Id == id && o.SellerId == sellerId, cancellationToken);

    public Task<bool> ExistsForSellerAsync(int sellerId, int productId, string condition, CancellationToken cancellationToken = default) =>
        DbSet.AnyAsync(o => o.SellerId == sellerId && o.ProductId == productId && o.Condition == condition, cancellationToken);

    public Task<int?> FindIdAsync(int sellerId, int productId, string condition, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking()
            .Where(o => o.SellerId == sellerId && o.ProductId == productId && o.Condition == condition)
            .Select(o => (int?)o.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<OfferPurchaseRow?> GetPurchaseRowAsync(int id, CancellationToken cancellationToken = default) =>
        // The soft-delete filter applies to the offer, its seller and its product (all inner joins),
        // so a deleted one of the three gives null: "not available".
        DbSet.AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OfferPurchaseRow(
                o.Id,
                o.SellerId,
                o.Price,
                o.Stock,
                o.ReservedStock,
                o.Seller.Status == ApprovedSeller && o.Product.Status == Approved && o.Product.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<List<Offer>> GetForUpdateAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return DbSet
            .IgnoreQueryFilters([PcPartsContext.SoftDeleteFilter])
            .Where(o => ids.Contains(o.Id))
            .Include(o => o.Product)
            .Include(o => o.Seller)
            .OrderBy(o => o.Id)
            .ToListAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------

    private static IQueryable<Offer> ApplyFilters(IQueryable<Offer> query, SellerOfferFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            // EF Core translates Contains to LIKE with the wildcards escaped.
            var keyword = filter.Keyword.Trim();
            query = query.Where(o => o.Product.Name.Contains(keyword) || o.Product.ModelNumber.Contains(keyword));
        }

        if (!string.IsNullOrEmpty(filter.Condition))
        {
            var condition = filter.Condition;
            query = query.Where(o => o.Condition == condition);
        }

        // Available = Stock - ReservedStock: what customers can still buy.
        return filter.Stock switch
        {
            OfferStockFilter.InStock => query.Where(o => o.Stock - o.ReservedStock > 0),
            OfferStockFilter.Low => query.Where(o => o.Stock - o.ReservedStock <= o.LowStockThreshold),
            OfferStockFilter.OutOfStock => query.Where(o => o.Stock - o.ReservedStock <= 0),
            _ => query
        };
    }

    /// <summary>
    /// Projection used by every seller read. Products are never hard-deleted, so the join to
    /// Product always finds the row; a deactivated product shows as ProductIsPublished = false.
    /// </summary>
    private static IQueryable<SellerOfferRow> ToRows(IQueryable<Offer> query) =>
        query.Select(o => new SellerOfferRow(
            o.Id,
            o.ProductId,
            o.Product.Name,
            o.Product.Slug,
            o.Product.ModelNumber,
            o.Product.ProductImages.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Url).FirstOrDefault(),
            o.Product.Status == Approved && o.Product.IsActive,
            o.Condition,
            o.Price,
            o.Stock,
            o.ReservedStock,
            o.LowStockThreshold,
            o.CreatedAt,
            o.UpdatedAt));
}

public class StockMovementRepository(PcPartsContext context) : Repository<StockMovement>(context), IStockMovementRepository
{
    public Task<PagedResult<StockMovement>> GetPagedByOfferAsync(int offerId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking()
            .Where(m => m.OfferId == offerId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .ToPagedResultAsync(page, pageSize, cancellationToken);
}
