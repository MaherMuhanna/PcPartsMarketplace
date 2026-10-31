using PCM.Infrastructure.DTO.Dashboards;
using PCM.Infrastructure.Exceptions;
using PCM.Repositories;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Services;

/// <summary>
/// Builds the dashboard numbers. All queries share the request's DbContext, so they run one after
/// another (a DbContext does not support parallel queries); each one is a single aggregate in SQL.
/// </summary>
public sealed class DashboardService(IUnitOfWork unitOfWork, TimeProvider timeProvider) : IDashboardService
{
    /// <summary>Window of the "orders" card on the admin dashboard.</summary>
    public const int OrdersWindowDays = 7;

    /// <summary>Window of the delivered-sales cards.</summary>
    public const int SalesWindowDays = 30;

    /// <summary>Rows in the seller's "recent orders" table.</summary>
    public const int RecentSubOrders = 5;

    public async Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dashboards = unitOfWork.Dashboards;

        var pendingSellers = await dashboards.CountPendingSellersAsync(cancellationToken);
        var pendingProposals = await dashboards.CountPendingProposalsAsync(cancellationToken);
        var byStatus = await dashboards.CountSubOrdersByStatusAsync(sellerId: null, cancellationToken);
        var recentOrders = await dashboards.CountOrdersCreatedSinceAsync(now.AddDays(-OrdersWindowDays), cancellationToken);
        var sales = await dashboards.GetDeliveredSalesAsync(sellerId: null, now.AddDays(-SalesWindowDays), cancellationToken);

        return new AdminDashboardDto(pendingSellers, pendingProposals, ToDto(byStatus), recentOrders, sales.Amount);
    }

    public async Task<SellerDashboardDto> GetSellerDashboardAsync(int userId, CancellationToken cancellationToken = default)
    {
        // The ApprovedSeller policy has already checked the profile; this only maps user -> seller id.
        var profile = await unitOfWork.SellerProfiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Seller profile was not found.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dashboards = unitOfWork.Dashboards;

        var byStatus = await dashboards.CountSubOrdersByStatusAsync(profile.Id, cancellationToken);
        var stock = await dashboards.GetOfferStockCountsAsync(profile.Id, cancellationToken);
        var sales = await dashboards.GetDeliveredSalesAsync(profile.Id, now.AddDays(-SalesWindowDays), cancellationToken);
        var recent = await dashboards.GetRecentSubOrdersAsync(profile.Id, RecentSubOrders, cancellationToken);

        return new SellerDashboardDto(
            ToDto(byStatus),
            stock.OfferCount,
            stock.LowStock,
            stock.OutOfStock,
            sales.Amount,
            sales.Count,
            [.. recent.Select(r => new RecentSubOrderDto(r.Id, r.OrderNumber, r.Status, r.Subtotal, r.CreatedAt))]);
    }

    private static SubOrderStatusCountsDto ToDto(SubOrderStatusCountsRow row) =>
        new(row.Pending, row.Confirmed, row.Shipped, row.Delivered, row.Cancelled);
}
