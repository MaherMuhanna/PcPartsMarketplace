using PCM.DomainEntities.DBEntities;
using PCM.Repositories.Common;
using PCM.Repositories.Models;

namespace PCM.Repositories.IRepository;

/// <summary>
/// Seller offers. Every seller-facing read is scoped to the owner: callers pass the seller
/// profile id resolved from the token, and another seller's offer is simply "not found".
/// </summary>
public interface IOfferRepository : IRepository<Offer>
{
    /// <summary>Read-only page of the seller's offers with product data; filters, sort and paging run in SQL.</summary>
    Task<PagedResult<SellerOfferRow>> GetSellerPagedAsync(
        int sellerId, SellerOfferFilter filter, int page, int pageSize, SellerOfferSort sort, CancellationToken cancellationToken = default);

    /// <summary>One of the seller's offers with product data (read-only), or null.</summary>
    Task<SellerOfferRow?> GetSellerRowAsync(int sellerId, int id, CancellationToken cancellationToken = default);

    /// <summary>Tracked offer if it exists and belongs to the seller, otherwise null.</summary>
    Task<Offer?> GetOwnedAsync(int sellerId, int id, CancellationToken cancellationToken = default);

    /// <summary>True if the seller already has a (non-deleted) offer for the product in this condition.</summary>
    Task<bool> ExistsForSellerAsync(int sellerId, int productId, string condition, CancellationToken cancellationToken = default);

    /// <summary>Id of the seller's (non-deleted) offer for the product in this condition, or null (demo seeding).</summary>
    Task<int?> FindIdAsync(int sellerId, int productId, string condition, CancellationToken cancellationToken = default);

    /// <summary>What a customer adding the offer to a cart needs to know; null if it does not exist or is deleted.</summary>
    Task<OfferPurchaseRow?> GetPurchaseRowAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked offers with Product and Seller, soft-deleted ones included (checkout must report them as
    /// unavailable, and a cancelled order must still release stock). Used by checkout and status changes.
    /// </summary>
    Task<List<Offer>> GetForUpdateAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
}

/// <summary>Inventory audit trail (append-only: rows are added through the offer, never changed).</summary>
public interface IStockMovementRepository : IRepository<StockMovement>
{
    /// <summary>Read-only page of one offer's movements, newest first.</summary>
    Task<PagedResult<StockMovement>> GetPagedByOfferAsync(int offerId, int page, int pageSize, CancellationToken cancellationToken = default);
}
