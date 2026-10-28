using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.Services;

public interface IBrandService
{
    /// <summary>Paged list of brands. The query is assumed to be validated already.</summary>
    Task<PagedResponse<BrandDto>> GetBrandsAsync(BrandListQuery query, CancellationToken cancellationToken = default);

    /// <summary>Creates a brand; 409 if the name or its slug is taken.</summary>
    Task<BrandDto> CreateAsync(BrandRequest request, CancellationToken cancellationToken = default);

    /// <summary>Renames a brand (slug follows the name) and sets its logo.</summary>
    Task<BrandDto> UpdateAsync(int id, BrandRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a brand; 409 while any product uses it.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
