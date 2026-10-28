using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.Services;

/// <summary>Admin management of master products, including the review of seller proposals.</summary>
public interface IProductAdminService
{
    Task<PagedResponse<ProductManageListItemDto>> GetProductsAsync(AdminProductListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Any product in any status; 404 if it does not exist.</summary>
    Task<ProductManageDto> GetProductAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a product that is Approved and active straight away.</summary>
    Task<ProductManageDto> CreateAsync(ProductUpsertRequest request, CancellationToken cancellationToken = default);

    /// <summary>Edits any product; its status does not change.</summary>
    Task<ProductManageDto> UpdateAsync(int id, ProductUpsertRequest request, CancellationToken cancellationToken = default);

    /// <summary>Shows (true) or hides (false) a product in the catalog without deleting it.</summary>
    Task<ProductManageDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Pending -> Approved. 409 from any other status.</summary>
    Task<ProductManageDto> ApproveAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Pending -> Rejected with a note for the seller. 409 from any other status.</summary>
    Task<ProductManageDto> RejectAsync(int id, string note, CancellationToken cancellationToken = default);
}
