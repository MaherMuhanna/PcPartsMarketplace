using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.Repositories.Common;
using PCM.Repositories.IRepository;

namespace PCM.Repositories.Repository;

public class BrandRepository(PcPartsContext context) : Repository<Brand>(context), IBrandRepository
{
    public Task<PagedResult<Brand>> GetPagedAsync(int page, int pageSize, BrandSort sort, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        // Id is the tie-breaker so paging is stable when sort keys are equal.
        var ordered = sort switch
        {
            BrandSort.NameDesc => query.OrderByDescending(b => b.Name).ThenBy(b => b.Id),
            BrandSort.Newest => query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id),
            _ => query.OrderBy(b => b.Name).ThenBy(b => b.Id)
        };

        return ordered.ToPagedResultAsync(page, pageSize, cancellationToken);
    }

    public async Task<IReadOnlyList<Brand>> GetBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking()
            .Where(b => slugs.Contains(b.Slug))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Brand>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
}
