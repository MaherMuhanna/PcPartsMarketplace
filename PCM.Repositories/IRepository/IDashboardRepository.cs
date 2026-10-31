using PCM.Repositories.Models;

namespace PCM.Repositories.IRepository;

/// <summary>
/// Read-only aggregates for the dashboards. Seller methods take the seller profile id resolved
/// from the token; admin methods pass null to count every seller.
/// </summary>
public interface IDashboardRepository
{
    /// <summary>Sub-orders per status, for one seller or (null) for the whole marketplace.</summary>
    Task<SubOrderStatusCountsRow> CountSubOrdersByStatusAsync(int? sellerId, CancellationToken cancellationToken = default);

    /// <summary>Delivered sub-orders with DeliveredAt at or after <paramref name="since"/> (UTC).</summary>
    Task<DeliveredSalesRow> GetDeliveredSalesAsync(int? sellerId, DateTime since, CancellationToken cancellationToken = default);

    /// <summary>Customer orders placed at or after <paramref name="since"/> (UTC).</summary>
    Task<int> CountOrdersCreatedSinceAsync(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>Seller sign-ups waiting for review.</summary>
    Task<int> CountPendingSellersAsync(CancellationToken cancellationToken = default);

    /// <summary>Product proposals waiting for review.</summary>
    Task<int> CountPendingProposalsAsync(CancellationToken cancellationToken = default);

    /// <summary>Offer, low-stock and out-of-stock counts of one seller (non-deleted offers).</summary>
    Task<OfferStockCountsRow> GetOfferStockCountsAsync(int sellerId, CancellationToken cancellationToken = default);

    /// <summary>The seller's newest sub-orders, newest first.</summary>
    Task<IReadOnlyList<RecentSubOrderRow>> GetRecentSubOrdersAsync(int sellerId, int take, CancellationToken cancellationToken = default);
}
