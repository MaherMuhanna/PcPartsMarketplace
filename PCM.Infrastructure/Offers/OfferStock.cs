using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;

namespace PCM.Infrastructure.Offers;

/// <summary>
/// The only place that changes Offer.Stock. Every change is applied to the offer and recorded
/// as a <see cref="StockMovement"/> in the same unit of work, so the audit trail always explains
/// the current numbers. Phase 5 (checkout) adds Reserve / Release / Sale on top of this.
/// </summary>
public static class OfferStock
{
    /// <summary>Available = Stock - ReservedStock: what customers can still buy.</summary>
    public static int Available(int stock, int reservedStock) => stock - reservedStock;

    /// <summary>Low stock = available at or below the seller's threshold (out of stock included).</summary>
    public static bool IsLow(int stock, int reservedStock, int lowStockThreshold) =>
        Available(stock, reservedStock) <= lowStockThreshold;

    /// <summary>
    /// Adds <paramref name="change"/> (signed) to the offer's stock and appends the movement to
    /// the offer, so it is inserted on the next SaveChanges.
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

        var movement = new StockMovement
        {
            Reason = reason.ToString(),
            Change = change,
            StockAfter = offer.Stock,
            ReservedStockAfter = offer.ReservedStock,
            CreatedByUserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };

        offer.StockMovements.Add(movement);
        return movement;
    }
}
