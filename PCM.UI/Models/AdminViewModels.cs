using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.UI.Models;

/// <summary>Admin > Orders list: status tabs, order-number search, sort and paging (all kept in the URL).</summary>
public sealed class AdminOrdersViewModel
{
    /// <summary>Status tabs, in display order (null = all). An order is listed if any of its shipments has the status.</summary>
    public static readonly string?[] StatusTabs = [null, "Pending", "Confirmed", "Shipped", "Delivered", "Cancelled"];

    public PagedResponse<OrderListItemDto> Orders { get; init; } = new([], 1, 20, 0, 0);

    public string? Status { get; init; }

    public string? Q { get; init; }

    /// <summary>Null = newest first; "oldest" = oldest first.</summary>
    public string? Sort { get; init; }
}
