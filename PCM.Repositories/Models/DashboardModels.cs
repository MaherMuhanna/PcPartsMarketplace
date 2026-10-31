namespace PCM.Repositories.Models;

// Read models for the admin and seller dashboards. Counts only, computed in SQL.

/// <summary>Number of sub-orders in each status (an order has no single status, its parts do).</summary>
public sealed record SubOrderStatusCountsRow(int Pending, int Confirmed, int Shipped, int Delivered, int Cancelled);

/// <summary>Delivered sub-orders in a period: how many and their total (cash collected on delivery).</summary>
public sealed record DeliveredSalesRow(int Count, decimal Amount);

/// <summary>
/// Inventory health of one seller. LowStock = Available &lt;= LowStockThreshold (same rule as the
/// low-stock list, so zero stock is included); OutOfStock = Available &lt;= 0 and is a subset of LowStock.
/// </summary>
public sealed record OfferStockCountsRow(int OfferCount, int LowStock, int OutOfStock);

/// <summary>A recent sub-order shown on the seller dashboard.</summary>
public sealed record RecentSubOrderRow(int Id, string OrderNumber, string Status, decimal Subtotal, DateTime CreatedAt);
