using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Offers;

namespace PCM.Infrastructure.Orders;

/// <summary>
/// Applies one status move to a tracked sub-order, with its side effects (business rules 4 to 6):
/// <list type="bullet">
///   <item>Confirmed / Shipped / Delivered / Cancelled set the matching timestamp.</item>
///   <item>Delivered: every line is a Sale (Stock and ReservedStock both drop).</item>
///   <item>Cancelled: every line is a Release (ReservedStock drops) and the reason is stored.</item>
/// </list>
/// Pure domain code: callers load the rows and save once, so the status, stock and movements
/// commit together or not at all.
/// </summary>
public static class SellerOrderWorkflow
{
    /// <summary>Longest reason a user can type; the stored text adds a short "Seller: " style prefix (column is 300).</summary>
    public const int MaxReasonLength = 250;

    /// <param name="sellerOrder">Tracked, with OrderItems loaded.</param>
    /// <param name="offers">Tracked offers of the sub-order's lines, by id (needed for Delivered and Cancelled).</param>
    /// <param name="to">Target status.</param>
    /// <param name="actor">Who makes the move (customers may only cancel).</param>
    /// <param name="reason">Cancel reason (required from sellers and admins; validated by the request validator).</param>
    /// <param name="userId">Current user, recorded on the stock movements.</param>
    /// <param name="now">UTC time of the move.</param>
    /// <exception cref="ConflictException">The move is not allowed from the current status.</exception>
    public static void Apply(
        SellerOrder sellerOrder,
        IReadOnlyDictionary<int, Offer> offers,
        SellerOrderStatus to,
        OrderActor actor,
        string? reason,
        int userId,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(sellerOrder);
        ArgumentNullException.ThrowIfNull(offers);

        var from = Enum.Parse<SellerOrderStatus>(sellerOrder.Status);
        SellerOrderTransitions.EnsureAllowed(from, to, actor);

        sellerOrder.Status = to.ToString();

        switch (to)
        {
            case SellerOrderStatus.Confirmed:
                sellerOrder.ConfirmedAt = now;
                break;

            case SellerOrderStatus.Shipped:
                sellerOrder.ShippedAt = now;
                break;

            case SellerOrderStatus.Delivered:
                sellerOrder.DeliveredAt = now;
                foreach (var item in sellerOrder.OrderItems)
                {
                    OfferStock.Sell(GetOffer(offers, item), sellerOrder, item.Quantity, userId);
                }

                break;

            case SellerOrderStatus.Cancelled:
                sellerOrder.CancelledAt = now;
                sellerOrder.CancelReason = FormatCancelReason(actor, reason);
                foreach (var item in sellerOrder.OrderItems)
                {
                    OfferStock.Release(GetOffer(offers, item), sellerOrder, item.Quantity, userId, $"Cancelled by {actor.ToString().ToLowerInvariant()}");
                }

                break;
        }
    }

    /// <summary>True when the move changes stock, so the caller must load the offers first.</summary>
    public static bool ChangesStock(SellerOrderStatus to) =>
        to is SellerOrderStatus.Delivered or SellerOrderStatus.Cancelled;

    /// <summary>
    /// The schema has no "cancelled by" column, so the reason records who cancelled:
    /// "Customer: Ordered the wrong size", "Seller: Out of stock in the warehouse".
    /// </summary>
    public static string FormatCancelReason(OrderActor actor, string? reason)
    {
        var text = string.IsNullOrWhiteSpace(reason) ? "No reason given" : reason.Trim();
        if (text.Length > MaxReasonLength)
        {
            text = text[..MaxReasonLength];
        }

        return $"{actor}: {text}";
    }

    private static Offer GetOffer(IReadOnlyDictionary<int, Offer> offers, OrderItem item) =>
        offers.TryGetValue(item.OfferId, out var offer)
            ? offer
            : throw new InvalidOperationException($"Offer {item.OfferId} of order item {item.Id} was not loaded.");
}
