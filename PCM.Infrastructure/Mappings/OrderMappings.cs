using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Offers;
using PCM.Infrastructure.Orders;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Mappings;

/// <summary>Manual mapping for the cart and orders.</summary>
public static class OrderMappings
{
    // ---- Cart ----

    /// <summary>Groups the lines by seller (in the order the customer added them) and flags problem lines.</summary>
    public static CartDto ToCartDto(this IReadOnlyList<CartLineRow> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var sellers = lines
            .GroupBy(l => (l.SellerId, l.StoreName))
            .Select(group =>
            {
                var items = group.Select(ToDto).ToList();
                return new CartSellerGroupDto(group.Key.SellerId, group.Key.StoreName, items, items.Sum(i => i.LineTotal));
            })
            .ToList();

        var all = sellers.SelectMany(s => s.Items).ToList();
        return new CartDto(
            sellers,
            all.Sum(i => i.Quantity),
            all.Sum(i => i.LineTotal),
            CanCheckout: all.Count > 0 && all.All(i => i.Issue is null));
    }

    public static CartItemDto ToDto(this CartLineRow line)
    {
        // An unavailable offer has nothing to sell, whatever its stock numbers say.
        var available = line.IsPurchasable ? Math.Max(0, OfferStock.Available(line.Stock, line.ReservedStock)) : 0;
        var issue = !line.IsPurchasable ? CartIssues.Unavailable
            : available < line.Quantity ? CartIssues.InsufficientStock
            : null;

        return new CartItemDto(
            line.Id,
            line.OfferId,
            line.ProductId,
            line.ProductName,
            line.ProductSlug,
            line.ImageUrl,
            line.Condition,
            line.Price,
            line.Quantity,
            available,
            line.Price * line.Quantity,
            issue);
    }

    // ---- Orders ----

    /// <param name="row">The order.</param>
    /// <param name="actor">Who is looking: decides NextStatuses and CanCancel.</param>
    /// <param name="customer">Customer details for seller and admin views; null in the customer's own view.</param>
    public static OrderDto ToDto(this OrderDetailsRow row, OrderActor actor, OrderCustomerDto? customer)
    {
        ArgumentNullException.ThrowIfNull(row);
        var sellerOrders = row.SellerOrders.Select(s => s.ToDto(actor)).ToList();

        return new OrderDto(
            row.Id,
            row.OrderNumber,
            row.Total,
            row.PaymentMethod,
            row.CreatedAt,
            row.UpdatedAt,
            customer,
            row.ShippingAddress.ToDto(),
            sellerOrders,
            CanCancel: sellerOrders.Any(s => s.NextStatuses.Contains(nameof(SellerOrderStatus.Cancelled))));
    }

    public static SellerOrderDto ToDto(this SellerOrderRow row, OrderActor actor) =>
        new(
            row.Id,
            row.SellerId,
            row.StoreName,
            row.Status,
            row.Subtotal,
            row.CreatedAt,
            row.ConfirmedAt,
            row.ShippedAt,
            row.DeliveredAt,
            row.CancelledAt,
            row.CancelReason,
            row.Items.Select(ToDto).ToList(),
            NextStatuses(row.Status, actor));

    /// <summary>The seller's own view of a sub-order: lines, delivery details and the next allowed moves.</summary>
    public static SellerOrderDetailsDto ToSellerDetailsDto(this SellerOrderRow row, string customerName) =>
        new(
            row.Id,
            row.OrderId,
            row.OrderNumber,
            row.Status,
            row.Subtotal,
            row.CreatedAt,
            row.ConfirmedAt,
            row.ShippedAt,
            row.DeliveredAt,
            row.CancelledAt,
            row.CancelReason,
            row.PaymentMethod,
            customerName,
            row.ShippingAddress.ToDto(),
            row.Items.Select(ToDto).ToList(),
            NextStatuses(row.Status, OrderActor.Seller));

    public static OrderListItemDto ToDto(this OrderListRow row, OrderCustomerDto? customer) =>
        new(
            row.Id,
            row.OrderNumber,
            row.Total,
            row.CreatedAt,
            row.ItemCount,
            row.SellerOrders.Select(s => new SellerOrderBriefDto(s.Id, s.StoreName, s.Status, s.Subtotal)).ToList(),
            customer);

    public static SellerOrderListItemDto ToDto(this SellerOrderListRow row, string customerName) =>
        new(row.Id, row.OrderId, row.OrderNumber, row.Status, row.Subtotal, row.ItemCount, customerName, row.City, row.CreatedAt);

    public static OrderItemDto ToDto(this OrderItemRow row) =>
        new(row.Id, row.OfferId, row.ProductId, row.ProductName, row.ProductSlug, row.Condition, row.UnitPrice, row.Quantity, row.LineTotal);

    public static ShippingAddressDto ToDto(this ShippingAddressRow row) =>
        new(row.Id, row.RecipientName, row.City, row.Street, row.Details, row.Phone);

    private static IReadOnlyList<string> NextStatuses(string status, OrderActor actor) =>
        SellerOrderTransitions.AllowedNext(Enum.Parse<SellerOrderStatus>(status), actor)
            .Select(s => s.ToString())
            .ToList();
}
