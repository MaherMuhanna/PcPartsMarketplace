using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.Services;

/// <summary>
/// Product proposals of the signed-in (approved) seller. A proposal is a Product row with
/// Status = Pending and ProposedBySellerId = the seller; an admin approves or rejects it.
/// Every method takes the user id from the token and only touches that seller's proposals (404 otherwise).
/// </summary>
public interface ISellerProposalService
{
    Task<PagedResponse<ProductManageListItemDto>> GetProposalsAsync(int userId, SellerProposalListQuery query, CancellationToken cancellationToken = default);

    Task<ProductManageDto> GetProposalAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a proposal in Pending status.</summary>
    Task<ProductManageDto> CreateAsync(int userId, ProductUpsertRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits a Pending or Rejected proposal; a rejected one goes back to Pending for a new review.
    /// Approved products belong to the catalog and can only be changed by admins (409).
    /// </summary>
    Task<ProductManageDto> UpdateAsync(int userId, int id, ProductUpsertRequest request, CancellationToken cancellationToken = default);
}
