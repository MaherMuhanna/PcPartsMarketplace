using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.Repositories.IRepository;

namespace PCM.Repositories.Repository;

public class CategoryRepository(PcPartsContext context) : Repository<Category>(context), ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
}
