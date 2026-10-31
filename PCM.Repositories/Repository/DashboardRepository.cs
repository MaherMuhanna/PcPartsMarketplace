using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.Enums;
using PCM.Repositories.IRepository;
using PCM.Repositories.Models;

namespace PCM.Repositories.Repository;

/// <summary>
/// Dashboard counts. Every query is AsNoTracking and aggregated in SQL (GROUP BY / COUNT / SUM),
/// so no rows are loaded into memory. Not a generic repository: it reads several tables.
/// </summary>
public sealed class DashboardRepository(PcPartsContext context) : IDashboardRepository
{
    // Compile-time constants, so EF inlines them as SQL literals.
    private const string Delivered = nameof(SellerOrderStatus.Delivered);
    private const string PendingSeller = nameof(SellerStatus.Pending);
    private const string PendingProduct = nameof(ProductStatus.Pending);

    public async Task<SubOrderStatusCountsRow> CountSubOrdersByStatusAsync(int? sellerId, CancellationToken cancellationToken = default)
    {
        var query = context.SellerOrders.AsNoTracking();
        if (sellerId is { } id)
        {
            query = query.Where(so => so.SellerId == id);
        }

        // One GROUP BY Status (uses IX_SellerOrder_Status_CreatedAt / IX_SellerOrder_SellerId_Status).
        var counts = await query
            .GroupBy(so => so.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        int Of(SellerOrderStatus status) => counts.GetValueOrDefault(status.ToString());

        return new SubOrderStatusCountsRow(
            Of(SellerOrderStatus.Pending),
            Of(SellerOrderStatus.Confirmed),
            Of(SellerOrderStatus.Shipped),
            Of(SellerOrderStatus.Delivered),
            Of(SellerOrderStatus.Cancelled));
    }

    public async Task<DeliveredSalesRow> GetDeliveredSalesAsync(int? sellerId, DateTime since, CancellationToken cancellationToken = default)
    {
        var query = context.SellerOrders.AsNoTracking()
            .Where(so => so.Status == Delivered && so.DeliveredAt >= since);
        if (sellerId is { } id)
        {
            query = query.Where(so => so.SellerId == id);
        }

        // Grouping by a constant gives COUNT and SUM in one round trip; no rows = no group.
        var row = await query
            .GroupBy(_ => 1)
            .Select(g => new DeliveredSalesRow(g.Count(), g.Sum(so => so.Subtotal)))
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? new DeliveredSalesRow(0, 0m);
    }

    public Task<int> CountOrdersCreatedSinceAsync(DateTime since, CancellationToken cancellationToken = default) =>
        context.Orders.AsNoTracking().CountAsync(o => o.CreatedAt >= since, cancellationToken);

    public Task<int> CountPendingSellersAsync(CancellationToken cancellationToken = default) =>
        context.SellerProfiles.AsNoTracking().CountAsync(s => s.Status == PendingSeller, cancellationToken);

    public Task<int> CountPendingProposalsAsync(CancellationToken cancellationToken = default) =>
        context.Products.AsNoTracking().CountAsync(p => p.Status == PendingProduct, cancellationToken);

    public async Task<OfferStockCountsRow> GetOfferStockCountsAsync(int sellerId, CancellationToken cancellationToken = default)
    {
        // Same Available rule as OfferRepository's stock filters (Available = Stock - ReservedStock).
        var row = await context.Offers.AsNoTracking()
            .Where(o => o.SellerId == sellerId)
            .GroupBy(_ => 1)
            .Select(g => new OfferStockCountsRow(
                g.Count(),
                g.Count(o => o.Stock - o.ReservedStock <= o.LowStockThreshold),
                g.Count(o => o.Stock - o.ReservedStock <= 0)))
            .FirstOrDefaultAsync(cancellationToken);

        return row ?? new OfferStockCountsRow(0, 0, 0);
    }

    public async Task<IReadOnlyList<RecentSubOrderRow>> GetRecentSubOrdersAsync(int sellerId, int take, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);

        return await context.SellerOrders.AsNoTracking()
            .Where(so => so.SellerId == sellerId)
            .OrderByDescending(so => so.CreatedAt)
            .ThenByDescending(so => so.Id)
            .Take(take)
            .Select(so => new RecentSubOrderRow(so.Id, so.Order.OrderNumber!, so.Status, so.Subtotal, so.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
