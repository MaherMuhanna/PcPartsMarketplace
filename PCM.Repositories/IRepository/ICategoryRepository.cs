using PCM.DomainEntities.DBEntities;

namespace PCM.Repositories.IRepository;

public interface ICategoryRepository : IRepository<Category>
{
    /// <summary>
    /// Every category, read-only, ordered by SortOrder then Name. The tree is small
    /// (tens of rows), so services build the hierarchy in memory from one query.
    /// </summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
}
