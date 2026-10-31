using PCM.DomainEntities.DBEntities;
using PCM.Repositories.Common;
using PCM.Repositories.Models;

namespace PCM.Repositories.IRepository;

/// <summary>
/// Orders (never deleted). Customer reads pass the customer id from the token, so another customer's
/// order is "not found"; admin reads pass null.
/// </summary>
public interface IOrderRepository : IRepository<Order>
{
    /// <summary>Read-only page of the customer's orders.</summary>
    Task<PagedResult<OrderListRow>> GetCustomerPagedAsync(
        int customerId, int page, int pageSize, bool newestFirst, CancellationToken cancellationToken = default);

    /// <summary>Read-only page of every order (admin).</summary>
    Task<PagedResult<OrderListRow>> GetAdminPagedAsync(
        AdminOrderFilter filter, int page, int pageSize, bool newestFirst, CancellationToken cancellationToken = default);

    /// <summary>Read-only order with every sub-order and line; scoped to the customer unless <paramref name="customerId"/> is null (admin).</summary>
    Task<OrderDetailsRow?> GetDetailsAsync(int id, int? customerId, CancellationToken cancellationToken = default);

    /// <summary>Tracked order with its sub-orders and their lines, for status changes; scoped like <see cref="GetDetailsAsync"/>.</summary>
    Task<Order?> GetForUpdateAsync(int id, int? customerId, CancellationToken cancellationToken = default);
}

/// <summary>Sub-orders. Seller reads pass the seller profile id resolved from the token.</summary>
public interface ISellerOrderRepository : IRepository<SellerOrder>
{
    /// <summary>Read-only page of the seller's sub-orders, optionally of one status (a CHECK value).</summary>
    Task<PagedResult<SellerOrderListRow>> GetSellerPagedAsync(
        int sellerId, string? status, int page, int pageSize, bool newestFirst, CancellationToken cancellationToken = default);

    /// <summary>Read-only sub-order of the seller with its lines and shipping address, or null.</summary>
    Task<SellerOrderRow?> GetSellerDetailsAsync(int sellerId, int id, CancellationToken cancellationToken = default);

    /// <summary>Tracked sub-order of the seller with its order and lines, or null.</summary>
    Task<SellerOrder?> GetOwnedForUpdateAsync(int sellerId, int id, CancellationToken cancellationToken = default);

    /// <summary>Tracked sub-order (any seller) with its order and lines, or null. For system jobs only.</summary>
    Task<SellerOrder?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ids of Pending sub-orders created before <paramref name="createdBefore"/> (UTC), oldest first,
    /// at most <paramref name="take"/>. Uses IX_SellerOrder_Status_CreatedAt.
    /// </summary>
    Task<IReadOnlyList<int>> GetPendingIdsCreatedBeforeAsync(DateTime createdBefore, int take, CancellationToken cancellationToken = default);
}
