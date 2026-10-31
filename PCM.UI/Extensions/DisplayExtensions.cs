using System.Globalization;

namespace PCM.UI.Extensions;

/// <summary>Formatting shared by the catalog views.</summary>
public static class DisplayExtensions
{
    /// <summary>Prices are stored without a currency; the store sells in Jordanian dinars.</summary>
    public const string Currency = "JOD";

    /// <summary>Shown when a product has no image.</summary>
    public const string PlaceholderImage = "/images/products/placeholder.svg";

    public static string ToPrice(this decimal amount) =>
        $"{Currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    /// <summary>Dates are stored in UTC and shown the same way on every page.</summary>
    public static string ToDisplayDate(this DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Bootstrap badge colour of a sub-order status.</summary>
    public static string OrderStatusBadge(this string status) => status switch
    {
        "Pending" => "text-bg-secondary",
        "Confirmed" => "text-bg-info",
        "Shipped" => "text-bg-primary",
        "Delivered" => "text-bg-success",
        "Cancelled" => "text-bg-danger",
        _ => "text-bg-light"
    };

    /// <summary>Button text for moving a sub-order to <paramref name="status"/>.</summary>
    public static string OrderActionLabel(this string status) => status switch
    {
        "Confirmed" => "Confirm order",
        "Shipped" => "Mark as shipped",
        "Delivered" => "Mark as delivered (cash received)",
        "Cancelled" => "Cancel",
        _ => status
    };
}
