using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Repositories.Common;
using PCM.Repositories.IRepository;
using PCM.Repositories.Models;

namespace PCM.Repositories.Repository;

/// <remarks>
/// Orders are history: they keep pointing at addresses, offers and seller profiles that may be
/// soft-deleted later. Every read therefore ignores the soft-delete filter, otherwise EF would
/// drop the order row through the required navigation (see AddDataAccess).
/// </remarks>
public class OrderRepository(PcPartsContext context) : Repository<Order>(context), IOrderRepository
{
    public Task<PagedResult<OrderListRow>> GetCustomerPagedAsync(
        int customerId, int page, int pageSize, bool newestFirst, CancellationToken cancellationToken = default) =>
        GetPagedAsync(History().Where(o => o.CustomerId == customerId), page, pageSize, newestFirst, cancellationToken);

    public Task<PagedResult<OrderListRow>> GetAdminPagedAsync(
        AdminOrderFilter filter, int page, int pageSize, bool newestFirst, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var query = History();

        if (!string.IsNullOrEmpty(filter.Status))
        {
            var status = filter.Status;
            query = query.Where(o => o.SellerOrders.Any(s => s.Status == status));
        }

        if (!string.IsNullOrWhiteSpace(filter.OrderNumber))
        {
            // EF Core translates Contains to LIKE with the wildcards escaped.
            var number = filter.OrderNumber.Trim();
            query = query.Where(o => o.OrderNumber!.Contains(number));
        }

        return GetPagedAsync(query, page, pageSize, newestFirst, cancellationToken);
    }

    public Task<OrderDetailsRow?> GetDetailsAsync(int id, int? customerId, CancellationToken cancellationToken = default)
    {
        var query = History().Where(o => o.Id == id);
        if (customerId is { } owner)
        {
            query = query.Where(o => o.CustomerId == owner);
        }

        return query
            .AsSplitQuery()
            .Select(o => new OrderDetailsRow(
                o.Id,
                o.OrderNumber!,
                o.CustomerId,
                o.Total,
                o.PaymentMethod,
                o.CreatedAt,
                o.UpdatedAt,
                new ShippingAddressRow(o.ShippingAddressId, o.ShipRecipientName, o.ShipCity, o.ShipStreet, o.ShipDetails, o.ShipPhone),
                o.SellerOrders
                    .OrderBy(s => s.Id)
                    .Select(s => new SellerOrderRow(
                        s.Id,
                        s.OrderId,
                        o.OrderNumber!,
                        o.CustomerId,
                        s.SellerId,
                        s.Seller.StoreName,
                        s.Status,
                        s.Subtotal,
                        o.PaymentMethod,
                        s.CreatedAt,
                        s.ConfirmedAt,
                        s.ShippedAt,
                        s.DeliveredAt,
                        s.CancelledAt,
                        s.CancelReason,
                        new ShippingAddressRow(o.ShippingAddressId, o.ShipRecipientName, o.ShipCity, o.ShipStreet, o.ShipDetails, o.ShipPhone),
                        s.OrderItems
                            .OrderBy(i => i.Id)
                            .Select(i => new OrderItemRow(
                                i.Id,
                                i.OfferId,
                                i.Offer.ProductId,
                                i.ProductName,
                                i.Offer.Product.Slug,
                                i.Condition,
                                i.UnitPrice,
                                i.Quantity,
                                i.LineTotal ?? i.UnitPrice * i.Quantity))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Order?> GetForUpdateAsync(int id, int? customerId, CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(o => o.Id == id);
        if (customerId is { } owner)
        {
            query = query.Where(o => o.CustomerId == owner);
        }

        return query
            .Include(o => o.SellerOrders)
            .ThenInclude(s => s.OrderItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------

    private IQueryable<Order> History() =>
        DbSet.AsNoTracking().IgnoreQueryFilters([PcPartsContext.SoftDeleteFilter]);

    private static async Task<PagedResult<OrderListRow>> GetPagedAsync(
        IQueryable<Order> query, int page, int pageSize, bool newestFirst, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var totalCount = await query.CountAsync(cancellationToken);

        // Id is the tie-breaker (and follows creation order), so paging is stable.
        var ordered = newestFirst
            ? query.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id)
            : query.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id);

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderListRow(
                o.Id,
                o.OrderNumber!,
                o.CustomerId,
                o.Total,
                o.CreatedAt,
                o.SellerOrders.SelectMany(s => s.OrderItems).Sum(i => i.Quantity),
                o.SellerOrders
                    .OrderBy(s => s.Id)
                    .Select(s => new SellerOrderBriefRow(s.Id, s.Seller.StoreName, s.Status, s.Subtotal))
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderListRow>(items, page, pageSize, totalCount);
    }
}

public class SellerOrderRepository(PcPartsContext context) : Repository<SellerOrder>(context), ISellerOrderRepository
{
    public async Task<PagedResult<SellerOrderListRow>> GetSellerPagedAsync(
        int sellerId, string? status, int page, int pageSize, bool newestFirst, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = History().Where(s => s.SellerId == sellerId);
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(s => s.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var ordered = newestFirst
            ? query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
            : query.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id);

        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SellerOrderListRow(
                s.Id,
                s.OrderId,
                s.Order.OrderNumber!,
                s.Order.CustomerId,
                s.Status,
                s.Subtotal,
                s.OrderItems.Sum(i => i.Quantity),
                s.Order.ShipCity,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<SellerOrderListRow>(items, page, pageSize, totalCount);
    }

    public Task<SellerOrderRow?> GetSellerDetailsAsync(int sellerId, int id, CancellationToken cancellationToken = default) =>
        History()
            .Where(s => s.Id == id && s.SellerId == sellerId)
            .AsSplitQuery()
            .Select(s => new SellerOrderRow(
                s.Id,
                s.OrderId,
                s.Order.OrderNumber!,
                s.Order.CustomerId,
                s.SellerId,
                s.Seller.StoreName,
                s.Status,
                s.Subtotal,
                s.Order.PaymentMethod,
                s.CreatedAt,
                s.ConfirmedAt,
                s.ShippedAt,
                s.DeliveredAt,
                s.CancelledAt,
                s.CancelReason,
                new ShippingAddressRow(
                    s.Order.ShippingAddressId,
                    s.Order.ShipRecipientName,
                    s.Order.ShipCity,
                    s.Order.ShipStreet,
                    s.Order.ShipDetails,
                    s.Order.ShipPhone),
                s.OrderItems
                    .OrderBy(i => i.Id)
                    .Select(i => new OrderItemRow(
                        i.Id,
                        i.OfferId,
                        i.Offer.ProductId,
                        i.ProductName,
                        i.Offer.Product.Slug,
                        i.Condition,
                        i.UnitPrice,
                        i.Quantity,
                        i.LineTotal ?? i.UnitPrice * i.Quantity))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SellerOrder?> GetOwnedForUpdateAsync(int sellerId, int id, CancellationToken cancellationToken = default) =>
        DbSet
            .Where(s => s.Id == id && s.SellerId == sellerId)
            .Include(s => s.Order)
            .Include(s => s.OrderItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SellerOrder?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        DbSet
            .Where(s => s.Id == id)
            .Include(s => s.Order)
            .Include(s => s.OrderItems)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetPendingIdsCreatedBeforeAsync(DateTime createdBefore, int take, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(take, 1);

        const string pending = nameof(SellerOrderStatus.Pending);
        return await DbSet
            .AsNoTracking()
            .Where(s => s.Status == pending && s.CreatedAt < createdBefore)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Select(s => s.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<SellerOrder> History() =>
        DbSet.AsNoTracking().IgnoreQueryFilters([PcPartsContext.SoftDeleteFilter]);
}
