namespace PCM.Repositories.Models;

// Read models for the cart and order screens. Projections only (no tracking).
// History rows keep pointing at soft-deleted principals (offer, address, seller profile), so every
// query that builds these ignores the soft-delete filter and reports the state explicitly.

/// <summary>
/// One cart line with the live offer data. IsPurchasable = offer not deleted, seller Approved,
/// product published. Stock and ReservedStock are the offer's current numbers.
/// </summary>
public sealed record CartLineRow(
    int Id,
    int OfferId,
    int ProductId,
    string ProductName,
    string ProductSlug,
    string? ImageUrl,
    int SellerId,
    string StoreName,
    string Condition,
    decimal Price,
    int Quantity,
    int Stock,
    int ReservedStock,
    bool IsPurchasable,
    DateTime CreatedAt);

/// <summary>What adding an offer to a cart needs to know about it (null when the offer does not exist or is deleted).</summary>
public sealed record OfferPurchaseRow(int Id, int SellerId, decimal Price, int Stock, int ReservedStock, bool IsPurchasable);

/// <summary>Shipping address of an order.</summary>
public sealed record ShippingAddressRow(int Id, string City, string Street, string? Details, string Phone);

/// <summary>One bought line (snapshot of name and price at checkout).</summary>
public sealed record OrderItemRow(
    int Id,
    int OfferId,
    int ProductId,
    string ProductName,
    string? ProductSlug,
    string Condition,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

/// <summary>One seller's part of an order, with its lines.</summary>
public sealed record SellerOrderRow(
    int Id,
    int OrderId,
    string OrderNumber,
    int CustomerId,
    int SellerId,
    string StoreName,
    string Status,
    decimal Subtotal,
    string PaymentMethod,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancelReason,
    ShippingAddressRow ShippingAddress,
    IReadOnlyList<OrderItemRow> Items);

/// <summary>A whole order with every sub-order and line.</summary>
public sealed record OrderDetailsRow(
    int Id,
    string OrderNumber,
    int CustomerId,
    decimal Total,
    string PaymentMethod,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    ShippingAddressRow ShippingAddress,
    IReadOnlyList<SellerOrderRow> SellerOrders);

/// <summary>Status of one sub-order in an order list row.</summary>
public sealed record SellerOrderBriefRow(int Id, string StoreName, string Status, decimal Subtotal);

/// <summary>One row of an order list (customer or admin).</summary>
public sealed record OrderListRow(
    int Id,
    string OrderNumber,
    int CustomerId,
    decimal Total,
    DateTime CreatedAt,
    int ItemCount,
    IReadOnlyList<SellerOrderBriefRow> SellerOrders);

/// <summary>One row of the seller's sub-order list.</summary>
public sealed record SellerOrderListRow(
    int Id,
    int OrderId,
    string OrderNumber,
    int CustomerId,
    string Status,
    decimal Subtotal,
    int ItemCount,
    string City,
    DateTime CreatedAt);

/// <summary>Filters of the admin order list (combined with AND).</summary>
public sealed record AdminOrderFilter
{
    /// <summary>Orders with at least one sub-order in this status (a CHECK value), or null for all.</summary>
    public string? Status { get; init; }

    /// <summary>Part of the order number, e.g. "000123" or "PCM-0001".</summary>
    public string? OrderNumber { get; init; }
}
