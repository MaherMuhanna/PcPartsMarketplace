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

    /// <summary>Read-only brands whose slug is in the list (unknown slugs are simply not returned).</summary>
    Task<IReadOnlyList<Brand>> GetBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default);

    /// <summary>Read-only brands by id (used to name brand facets).</summary>
    Task<IReadOnlyList<Brand>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
}
