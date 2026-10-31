using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.UI.Models;

/// <summary>Checkout page: the cart as it is now and the customer's addresses.</summary>
public sealed class CheckoutViewModel
{
    public required CartDto Cart { get; init; }

    public required IReadOnlyList<AddressDto> Addresses { get; init; }

    /// <summary>Pre-selected address: the default one.</summary>
    public int? SelectedAddressId { get; init; }
}

/// <summary>
/// Posted by the checkout page: the chosen address and every line exactly as shown
/// (hidden fields), so the API can tell whether anything changed in between.
/// </summary>
public sealed class CheckoutFormModel
{
    public int AddressId { get; set; }

    public List<CheckoutLineFormModel> Items { get; set; } = [];
}

public sealed class CheckoutLineFormModel
{
    public int CartItemId { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
}

/// <summary>"My orders" list.</summary>
public sealed class OrdersViewModel
{
    public PagedResponse<OrderListItemDto> Orders { get; init; } = new([], 1, 10, 0, 0);
}

/// <summary>The seller's sub-order list with status tabs.</summary>
public sealed class SellerOrdersViewModel
{
    /// <summary>Status tabs in display order: query value (null = all) and label.</summary>
    public static readonly (string? Value, string Label)[] StatusTabs =
        [(null, "All"), ("Pending", "New"), ("Confirmed", "Confirmed"), ("Shipped", "Shipped"), ("Delivered", "Delivered"), ("Cancelled", "Cancelled")];

    public PagedResponse<SellerOrderListItemDto> Orders { get; init; } = new([], 1, 20, 0, 0);

    public string? Status { get; init; }
}
