using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Offers;

namespace PCM.Infrastructure.Services;

/// <summary>
/// Offers and inventory of the signed-in (approved) seller. Every method takes the user id from
/// the token and only touches that seller's offers; another seller's offer id is "not found" (404).
/// Stock changes go through <see cref="ChangeStockAsync"/> (or creation) and always write a StockMovement.
/// </summary>
public interface ISellerOfferService
{
    Task<PagedResponse<OfferDto>> GetOffersAsync(int userId, SellerOfferListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="GetOffersAsync"/> with the stock filter forced to "low".</summary>
    Task<PagedResponse<OfferDto>> GetLowStockAsync(int userId, SellerOfferListQuery query, CancellationToken cancellationToken = default);

    Task<OfferDto> GetOfferAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an offer on a published product. One offer per product and condition (409).
    /// Initial stock above 0 is recorded as a Restock movement.
    /// </summary>
    Task<OfferDto> CreateAsync(int userId, CreateOfferRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes price and low-stock threshold. Retries once on a concurrency conflict, then 409.</summary>
    Task<OfferDto> UpdateAsync(int userId, int id, UpdateOfferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restock or Adjustment with a StockMovement. Stock may never drop below the reserved quantity (409).
    /// Retries once on a concurrency conflict (e.g. an order reserved stock at the same moment), then 409.
    /// </summary>
    Task<OfferDto> ChangeStockAsync(int userId, int id, StockChangeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the offer. Not allowed while open orders hold reserved stock (409).</summary>
    Task DeleteAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>Inventory history of one of the seller's offers, newest first.</summary>
    Task<PagedResponse<StockMovementDto>> GetMovementsAsync(int userId, int id, StockMovementListQuery query, CancellationToken cancellationToken = default);
}
