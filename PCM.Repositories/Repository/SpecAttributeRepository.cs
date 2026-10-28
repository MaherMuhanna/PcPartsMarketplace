using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.Repositories.IRepository;

namespace PCM.Repositories.Repository;

public class SpecAttributeRepository(PcPartsContext context) : Repository<SpecAttribute>(context), ISpecAttributeRepository
{
    public async Task<IReadOnlyList<SpecAttribute>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking()
            .Where(a => a.CategoryId == categoryId)
            .Include(a => a.SpecOptions.OrderBy(o => o.SortOrder).ThenBy(o => o.Value))
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public Task<SpecAttribute?> GetWithOptionsAsync(int id, CancellationToken cancellationToken = default) =>
        DbSet.Include(a => a.SpecOptions.OrderBy(o => o.SortOrder).ThenBy(o => o.Value))
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> IsInUseAsync(int attributeId, CancellationToken cancellationToken = default) =>
        Context.ProductSpecValues.AnyAsync(v => v.SpecAttributeId == attributeId, cancellationToken);

    public Task<bool> IsOptionInUseAsync(int optionId, CancellationToken cancellationToken = default) =>
        Context.ProductSpecValues.AnyAsync(v => v.SpecOptionId == optionId, cancellationToken);
}
