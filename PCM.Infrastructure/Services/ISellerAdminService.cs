using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Sellers;

namespace PCM.Infrastructure.Services;

/// <summary>Admin review of sellers (see <see cref="SellerStatusTransitions"/> for the allowed changes).</summary>
public interface ISellerAdminService
{
    Task<PagedResponse<SellerAdminDto>> GetSellersAsync(SellerListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Pending to Approved.</summary>
    Task<SellerAdminDto> ApproveAsync(int sellerId, CancellationToken cancellationToken = default);

    /// <summary>Pending to Rejected; revokes the seller's refresh tokens.</summary>
    Task<SellerAdminDto> RejectAsync(int sellerId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Approved to Suspended; revokes the seller's refresh tokens.</summary>
    Task<SellerAdminDto> SuspendAsync(int sellerId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Suspended to Approved.</summary>
    Task<SellerAdminDto> ReinstateAsync(int sellerId, CancellationToken cancellationToken = default);
}
