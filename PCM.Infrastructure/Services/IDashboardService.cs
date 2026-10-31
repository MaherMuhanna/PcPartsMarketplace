using PCM.Infrastructure.DTO.Dashboards;

namespace PCM.Infrastructure.Services;

/// <summary>Summary numbers for the admin and seller home pages.</summary>
public interface IDashboardService
{
    /// <summary>Marketplace-wide counts: review queues, sub-orders per status, recent orders and sales.</summary>
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>The signed-in seller's numbers. The seller comes from the token (<paramref name="userId"/>).</summary>
    Task<SellerDashboardDto> GetSellerDashboardAsync(int userId, CancellationToken cancellationToken = default);
}
