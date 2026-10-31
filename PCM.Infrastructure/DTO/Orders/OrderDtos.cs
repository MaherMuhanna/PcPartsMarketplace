using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.DTO.Orders;

// ---------------------------------------------------------------------
// Checkout
// ---------------------------------------------------------------------

/// <summary>
/// Body of POST /api/v1/orders/checkout. Items are the cart lines exactly as the customer saw them
/// on the checkout page (price and quantity). The API compares them with the live cart and offers;
/// any difference is a 409 with the affected lines, so nobody pays a price they did not see.
/// </summary>
public sealed record CheckoutRequest
{
    /// <summary>One of the customer's own addresses.</summary>
    public int AddressId { get; init; }

    public IReadOnlyList<CheckoutItemRequest> Items { get; init; } = [];
}

/// <summary>One cart line as shown on the checkout page.</summary>
public sealed record CheckoutItemRequest
{
    public int CartItemId { get; init; }

    /// <summary>Unit price the customer saw.</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>Quantity the customer saw.</summary>
    public int Quantity { get; init; }
}

/// <summary>Problem codes of a checkout conflict line.</summary>
public static class CheckoutProblems
{
    /// <summary>The offer's price is not the one the customer saw.</summary>
    public const string PriceChanged = "PriceChanged";

    /// <summary>Fewer units are available than requested.</summary>
    public const string InsufficientStock = "InsufficientStock";

    /// <summary>The offer can no longer be bought (deleted, seller not approved, product unpublished).</summary>
    public const string Unavailable = "Unavailable";

    /// <summary>The cart changed since the checkout page was opened (line added, removed or quantity changed).</summary>
    public const string CartChanged = "CartChanged";
}

/// <summary>
/// One line of a 409 checkout response, in the ProblemDetails extension "items".
/// Fields that do not apply to the problem are null (e.g. no current price for a line that left the cart).
/// </summary>
public sealed record CheckoutIssueDto(
    int CartItemId,
    int? OfferId,
    string? ProductName,
    string Problem,
    string Message,
    decimal? ExpectedPrice,
    decimal? CurrentPrice,
    int? RequestedQuantity,
    int? Available);

// ---------------------------------------------------------------------
// Orders
// ---------------------------------------------------------------------

/// <summary>Shipping address of an order (the address row as it is now; see decisions log, Batch 4).</summary>
public sealed record ShippingAddressDto(int Id, string City, string Street, string? Details, string Phone);

/// <summary>The customer of an order. Only in seller and admin views.</summary>
public sealed record OrderCustomerDto(int Id, string FullName, string? Email);

/// <summary>One bought line: a snapshot of the product name and price at checkout (business rule 8).</summary>
public sealed record OrderItemDto(
    int Id,
    int OfferId,
    int ProductId,
    string ProductName,
    string? ProductSlug,
    string Condition,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);

/// <summary>
/// One seller's part of an order. NextStatuses lists the moves the caller may make right now
/// (customer: only "Cancelled" while Pending or Confirmed).
/// </summary>
public sealed record SellerOrderDto(
    int Id,
    int SellerId,
    string StoreName,
    string Status,
    decimal Subtotal,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancelReason,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<string> NextStatuses);

/// <summary>
/// A whole order: one per checkout, split into one sub-order per seller.
/// Total = sum of the sub-order subtotals (cancelled parts included: it is what was ordered).
/// Customer is null in the customer's own view. CanCancel: at least one part can still be cancelled by the caller.
/// </summary>
public sealed record OrderDto(
    int Id,
    string OrderNumber,
    decimal Total,
    string PaymentMethod,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    OrderCustomerDto? Customer,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<SellerOrderDto> SellerOrders,
    bool CanCancel);

/// <summary>Status of one sub-order in an order list row.</summary>
public sealed record SellerOrderBriefDto(int Id, string StoreName, string Status, decimal Subtotal);

/// <summary>One row of the customer's (or the admin's) order list.</summary>
public sealed record OrderListItemDto(
    int Id,
    string OrderNumber,
    decimal Total,
    DateTime CreatedAt,
    int ItemCount,
    IReadOnlyList<SellerOrderBriefDto> SellerOrders,
    OrderCustomerDto? Customer);

/// <summary>One row of the seller's order list (their sub-orders only).</summary>
public sealed record SellerOrderListItemDto(
    int Id,
    int OrderId,
    string OrderNumber,
    string Status,
    decimal Subtotal,
    int ItemCount,
    string CustomerName,
    string City,
    DateTime CreatedAt);

/// <summary>
/// A sub-order as its seller sees it: the lines to pack, who to deliver to, and the next allowed moves.
/// </summary>
public sealed record SellerOrderDetailsDto(
    int Id,
    int OrderId,
    string OrderNumber,
    string Status,
    decimal Subtotal,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? ShippedAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    string? CancelReason,
    string PaymentMethod,
    string CustomerName,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items,
    IReadOnlyList<string> NextStatuses);

// ---------------------------------------------------------------------
// Requests and queries
// ---------------------------------------------------------------------

/// <summary>Body of the customer's cancel endpoints. The reason is optional for customers.</summary>
public sealed record CancelOrderRequest
{
    public string? Reason { get; init; }
}

/// <summary>
/// Body of PATCH /api/v1/seller/orders/{id}/status and the admin equivalent.
/// Status: Confirmed, Shipped, Delivered or Cancelled. Reason is required when cancelling.
/// </summary>
public sealed record ChangeSellerOrderStatusRequest
{
    public string Status { get; init; } = string.Empty;

    public string? Reason { get; init; }
}

/// <summary>GET /api/v1/orders. Sort: newest (default) or oldest.</summary>
public sealed class OrderListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest", "oldest"];
}

/// <summary>GET /api/v1/seller/orders. Status filters the sub-orders; sort: newest (default) or oldest.</summary>
public sealed class SellerOrderListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest", "oldest"];

    public string? Status { get; init; }
}

/// <summary>
/// GET /api/v1/admin/orders. Status: orders with at least one sub-order in that status.
/// Q: part of the order number (e.g. "000123"). Sort: newest (default) or oldest.
/// </summary>
public sealed class AdminOrderListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest", "oldest"];

    public string? Status { get; init; }

    public string? Q { get; init; }
}
