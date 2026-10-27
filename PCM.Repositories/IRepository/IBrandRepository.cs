using PCM.DomainEntities.DBEntities;
using PCM.Repositories.Common;

namespace PCM.Repositories.IRepository;

/// <summary>Sort orders supported by brand listing.</summary>
public enum BrandSort
{
    NameAsc,
    NameDesc,
    Newest
}

public interface IBrandRepository : IRepository<Brand>
{
    /// <summary>Read-only (no tracking) page of brands.</summary>
    Task<PagedResult<Brand>> GetPagedAsync(int page, int pageSize, BrandSort sort, CancellationToken cancellationToken = default);
}
