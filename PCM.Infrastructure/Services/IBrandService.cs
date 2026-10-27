using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.Services;

public interface IBrandService
{
    /// <summary>Paged list of brands. The query is assumed to be validated already.</summary>
    Task<PagedResponse<BrandDto>> GetBrandsAsync(BrandListQuery query, CancellationToken cancellationToken = default);
}
