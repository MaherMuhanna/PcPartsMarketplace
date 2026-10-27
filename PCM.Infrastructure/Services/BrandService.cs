using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;
using PCM.Repositories.IRepository;

namespace PCM.Infrastructure.Services;

public sealed class BrandService(IUnitOfWork unitOfWork) : IBrandService
{
    public async Task<PagedResponse<BrandDto>> GetBrandsAsync(BrandListQuery query, CancellationToken cancellationToken = default)
    {
        var page = await unitOfWork.Brands.GetPagedAsync(query.Page, query.PageSize, ParseSort(query.Sort), cancellationToken);
        return page.ToResponse(brand => brand.ToDto());
    }

    /// <summary>Maps the public sort key to the repository sort order (default: by name).</summary>
    internal static BrandSort ParseSort(string? sort) => sort?.ToLowerInvariant() switch
    {
        "-name" => BrandSort.NameDesc,
        "newest" => BrandSort.Newest,
        _ => BrandSort.NameAsc
    };
}
