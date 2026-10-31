namespace PCM.Infrastructure.DTO.Dashboards;

/// <summary>Sub-orders per status. Orders are counted by their parts, because one order can be in several states.</summary>
public sealed record SubOrderStatusCountsDto(int Pending, int Confirmed, int Shipped, int Delivered, int Cancelled);

/// <summary>
/// GET /api/v1/admin/dashboard. Sales = Delivered sub-orders only (cash on delivery: the money is
/// collected when the parcel is delivered), summed by DeliveredAt over the last 30 days.
/// </summary>
public sealed record AdminDashboardDto(
    int PendingSellers,
    int PendingProposals,
    SubOrderStatusCountsDto SubOrdersByStatus,
    int OrdersLast7Days,
    decimal DeliveredSalesLast30Days);

/// <summary>
/// GET /api/v1/seller/dashboard. LowStockCount matches GET /seller/offers/low-stock
/// (Available &lt;= threshold, zero included); OutOfStockCount (Available = 0) is a subset of it.
/// </summary>
public sealed record SellerDashboardDto(
    SubOrderStatusCountsDto SubOrdersByStatus,
    int OfferCount,
    int LowStockCount,
    int OutOfStockCount,
    decimal DeliveredSalesLast30Days,
    int DeliveredOrdersLast30Days,
    IReadOnlyList<RecentSubOrderDto> RecentSubOrders);

/// <summary>One of the seller's newest sub-orders.</summary>
public sealed record RecentSubOrderDto(int Id, string OrderNumber, string Status, decimal Subtotal, DateTime CreatedAt);
