namespace PCM.Infrastructure.DTO.Carts;

/// <summary>Problems a cart line can have. The UI shows them next to the line; checkout is blocked while any exists.</summary>
public static class CartIssues
{
    /// <summary>The offer was deleted, its seller is no longer approved, or its product was unpublished.</summary>
    public const string Unavailable = "Unavailable";

    /// <summary>Fewer units are available than the line asks for.</summary>
    public const string InsufficientStock = "InsufficientStock";
}

/// <summary>
/// The signed-in customer's cart, grouped by seller (each group becomes one sub-order at checkout).
/// Prices are always the offers' current prices. Total includes every line; CanCheckout is false
/// when the cart is empty or any line has an issue.
/// </summary>
public sealed record CartDto(
    IReadOnlyList<CartSellerGroupDto> Sellers,
    int ItemCount,
    decimal Total,
    bool CanCheckout);

/// <summary>The lines of one seller.</summary>
public sealed record CartSellerGroupDto(
    int SellerId,
    string StoreName,
    IReadOnlyList<CartItemDto> Items,
    decimal Subtotal);

/// <summary>One cart line. Issue is null when the line can be bought (see <see cref="CartIssues"/>).</summary>
public sealed record CartItemDto(
    int Id,
    int OfferId,
    int ProductId,
    string ProductName,
    string ProductSlug,
    string? ImageUrl,
    string Condition,
    decimal UnitPrice,
    int Quantity,
    int Available,
    decimal LineTotal,
    string? Issue);

/// <summary>GET /api/v1/cart/summary: what the navigation badge needs (total units in the cart).</summary>
public sealed record CartSummaryDto(int ItemCount);

/// <summary>Body of POST /api/v1/cart/items. Adding an offer already in the cart increases its quantity.</summary>
public sealed record AddCartItemRequest
{
    public int OfferId { get; init; }

    /// <summary>Units to add, 1 to 10. The line total may not exceed 10 or the available stock.</summary>
    public int Quantity { get; init; } = 1;
}

/// <summary>Body of PUT /api/v1/cart/items/{id}: the new quantity of the line.</summary>
public sealed record UpdateCartItemRequest
{
    /// <summary>1 to 10, and not above the available stock.</summary>
    public int Quantity { get; init; }
}
