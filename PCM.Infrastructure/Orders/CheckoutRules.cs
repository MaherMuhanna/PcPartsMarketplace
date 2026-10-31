using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Offers;

namespace PCM.Infrastructure.Orders;

/// <summary>
/// Business rule 1: before an order is created, every cart line is re-read and compared with what
/// the customer saw on the checkout page. Pure function, so every case is unit-tested without a database.
/// </summary>
public static class CheckoutRules
{
    /// <summary>
    /// Returns one issue per problem line; an empty list means the checkout may go ahead.
    /// Checks, per line: still in the request (and nothing extra in it), offer purchasable,
    /// same quantity, same price, enough available stock.
    /// </summary>
    /// <param name="cartItems">The live cart lines (read inside the checkout transaction).</param>
    /// <param name="offers">Tracked offers of those lines with Product and Seller, by id (deleted ones included).</param>
    /// <param name="expected">The lines as the customer saw them.</param>
    public static IReadOnlyList<CheckoutIssueDto> FindIssues(
        IReadOnlyList<CartItem> cartItems,
        IReadOnlyDictionary<int, Offer> offers,
        IReadOnlyList<CheckoutItemRequest> expected)
    {
        ArgumentNullException.ThrowIfNull(cartItems);
        ArgumentNullException.ThrowIfNull(offers);
        ArgumentNullException.ThrowIfNull(expected);

        var issues = new List<CheckoutIssueDto>();
        var seen = expected.GroupBy(e => e.CartItemId).ToDictionary(g => g.Key, g => g.First());

        foreach (var item in cartItems.OrderBy(i => i.Id))
        {
            offers.TryGetValue(item.OfferId, out var offer);
            var name = offer?.Product?.Name;
            var available = offer is null ? 0 : Math.Max(0, OfferStock.Available(offer.Stock, offer.ReservedStock));

            if (!seen.TryGetValue(item.Id, out var line))
            {
                issues.Add(new CheckoutIssueDto(item.Id, item.OfferId, name, CheckoutProblems.CartChanged,
                    "This item was added to your cart after you opened the checkout page.",
                    null, offer?.Price, item.Quantity, available));
                continue;
            }

            if (offer is null || !OfferStock.IsPurchasable(offer))
            {
                issues.Add(new CheckoutIssueDto(item.Id, item.OfferId, name, CheckoutProblems.Unavailable,
                    "This offer is no longer available. Remove it from your cart.",
                    line.UnitPrice, null, item.Quantity, 0));
                continue;
            }

            if (line.Quantity != item.Quantity)
            {
                issues.Add(new CheckoutIssueDto(item.Id, item.OfferId, name, CheckoutProblems.CartChanged,
                    $"The quantity changed to {item.Quantity} after you opened the checkout page.",
                    line.UnitPrice, offer.Price, item.Quantity, available));
                continue;
            }

            if (offer.Price != line.UnitPrice)
            {
                issues.Add(new CheckoutIssueDto(item.Id, item.OfferId, name, CheckoutProblems.PriceChanged,
                    FormattableString.Invariant($"The price changed from {line.UnitPrice:0.00} to {offer.Price:0.00}."),
                    line.UnitPrice, offer.Price, item.Quantity, available));
                continue;
            }

            if (available < item.Quantity)
            {
                issues.Add(new CheckoutIssueDto(item.Id, item.OfferId, name, CheckoutProblems.InsufficientStock,
                    available == 0 ? "This offer is out of stock." : $"Only {available} available; you asked for {item.Quantity}.",
                    line.UnitPrice, offer.Price, item.Quantity, available));
            }
        }

        // Lines the page showed that are no longer in the cart (removed in another tab).
        var cartIds = cartItems.Select(i => i.Id).ToHashSet();
        foreach (var gone in seen.Values.Where(e => !cartIds.Contains(e.CartItemId)).OrderBy(e => e.CartItemId))
        {
            issues.Add(new CheckoutIssueDto(gone.CartItemId, null, null, CheckoutProblems.CartChanged,
                "This item is no longer in your cart.",
                gone.UnitPrice, null, gone.Quantity, null));
        }

        return issues;
    }
}
