using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;

namespace PCM.Infrastructure.Offers;

/// <summary>
/// The only place that changes Offer.Stock and Offer.ReservedStock. Every change is applied to the
/// offer and recorded as a <see cref="StockMovement"/> in the same unit of work, so the audit trail
/// always explains the current numbers.
/// </summary>
/// <remarks>
/// Meaning of <see cref="StockMovement.Change"/> per reason (always the column that moved):
/// <list type="bullet">
///   <item>Restock / Adjustment: change of Stock (seller, Phase 4).</item>
///   <item>Reserve: +quantity on ReservedStock (checkout).</item>
///   <item>Release: -quantity on ReservedStock (sub-order cancelled).</item>
///   <item>Sale: -quantity on Stock; ReservedStock drops by the same amount (sub-order delivered).</item>
/// </list>
/// </remarks>
public static class OfferStock
{
    /// <summary>Available = Stock - ReservedStock: what customers can still buy.</summary>
    public static int Available(int stock, int reservedStock) => stock - reservedStock;

    /// <summary>Low stock = available at or below the seller's threshold (out of stock included).</summary>
    public static bool IsLow(int stock, int reservedStock, int lowStockThreshold) =>
        Available(stock, reservedStock) <= lowStockThreshold;

    /// <summary>
    /// True when customers may buy from the offer right now: the offer is not deleted, its seller is
    /// Approved, and its product is published (Approved, active, not deleted). Stock is checked separately.
    /// The offer must be loaded with Product and Seller.
    /// </summary>
    public static bool IsPurchasable(Offer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return !offer.IsDeleted
            && offer.Seller is { IsDeleted: false, Status: nameof(SellerStatus.Approved) }
            && offer.Product is { IsDeleted: false, IsActive: true, Status: nameof(ProductStatus.Approved) };
    }

    /// <summary>
    /// Adds <paramref name="change"/> (signed) to the offer's stock and appends the movement to
    /// the offer, so it is inserted on the next SaveChanges. Used for Restock and Adjustment.
    /// </summary>
    /// <exception cref="ConflictException">The new stock would be negative or below the reserved quantity.</exception>
    public static StockMovement Apply(Offer offer, StockMovementReason reason, int change, int userId, string? note)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (change == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(change), "A stock movement cannot be zero.");
        }

        var newStock = (long)offer.Stock + change;
        if (newStock < 0)
        {
            throw new ConflictException($"Stock is {offer.Stock}; you cannot remove {-change}.");
        }

        // Reserved units belong to placed orders: they must stay in stock until shipped or released.
        if (newStock < offer.ReservedStock)
        {
            throw new ConflictException(
                $"Stock cannot go below the {offer.ReservedStock} unit(s) reserved by open orders " +
                $"(stock {offer.Stock}, change {change}, result {newStock}).");
        }

        if (newStock > int.MaxValue)
        {
            throw new ConflictException("Stock would exceed the largest supported quantity.");
        }

        offer.Stock = (int)newStock;
        return Record(offer, reason, change, userId, note, sellerOrder: null);
    }

    /// <summary>
    /// Checkout: holds <paramref name="quantity"/> units for a new sub-order (ReservedStock goes up,
    /// Stock is unchanged until delivery). The movement is linked to the sub-order.
    /// </summary>
    /// <exception cref="ConflictException">Fewer than <paramref name="quantity"/> units are available.</exception>
    public static StockMovement Reserve(Offer offer, SellerOrder sellerOrder, int quantity, int userId)
    {
        ArgumentNullException.ThrowIfNull(offer);
        EnsurePositive(quantity);

        var available = Available(offer.Stock, offer.ReservedStock);
        if (quantity > available)
        {
            throw new ConflictException($"Only {available} unit(s) of offer {offer.Id} are available; {quantity} requested.");
        }

        offer.ReservedStock += quantity;
        return Record(offer, StockMovementReason.Reserve, quantity, userId, note: null, sellerOrder);
    }

    /// <summary>Sub-order cancelled: gives the reserved units back to "available" (ReservedStock goes down).</summary>
    /// <exception cref="InvalidOperationException">Less is reserved than the sub-order holds (data is inconsistent).</exception>
    public static StockMovement Release(Offer offer, SellerOrder sellerOrder, int quantity, int? userId, string? note)
    {
        ArgumentNullException.ThrowIfNull(offer);
        EnsurePositive(quantity);
        EnsureReserved(offer, quantity);

        offer.ReservedStock -= quantity;
        return Record(offer, StockMovementReason.Release, -quantity, userId, note, sellerOrder);
    }

    /// <summary>
    /// Sub-order delivered: the reserved units leave the warehouse for good, so Stock and
    /// ReservedStock both drop by <paramref name="quantity"/> (business rule 5). Available is unchanged.
    /// </summary>
    /// <exception cref="InvalidOperationException">Less is reserved than the sub-order holds (data is inconsistent).</exception>
    public static StockMovement Sell(Offer offer, SellerOrder sellerOrder, int quantity, int? userId)
    {
        ArgumentNullException.ThrowIfNull(offer);
        EnsurePositive(quantity);
        EnsureReserved(offer, quantity);

        offer.Stock -= quantity;
        offer.ReservedStock -= quantity;
        return Record(offer, StockMovementReason.Sale, -quantity, userId, note: null, sellerOrder);
    }

    // ---------------------------------------------------------------------

    /// <remarks><paramref name="userId"/> is null when the system makes the change (auto-cancel job).</remarks>
    private static StockMovement Record(
        Offer offer, StockMovementReason reason, int change, int? userId, string? note, SellerOrder? sellerOrder)
    {
        var movement = new StockMovement
        {
            Reason = reason.ToString(),
            Change = change,
            StockAfter = offer.Stock,
            ReservedStockAfter = offer.ReservedStock,
            CreatedByUserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            // Navigation, not the id: at checkout the sub-order is new and gets its id on SaveChanges.
            SellerOrder = sellerOrder
        };

        offer.StockMovements.Add(movement);
        return movement;
    }

    private static void EnsurePositive(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }
    }

    /// <summary>Reserved units are only ever added by checkout, so a shortfall here is a bug, not a user error (500).</summary>
    private static void EnsureReserved(Offer offer, int quantity)
    {
        if (offer.ReservedStock < quantity || offer.Stock < quantity)
        {
            throw new InvalidOperationException(
                $"Offer {offer.Id} has {offer.ReservedStock} reserved and {offer.Stock} in stock; cannot release or sell {quantity}.");
        }
    }
}
