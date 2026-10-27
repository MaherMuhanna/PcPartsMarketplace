using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;

namespace PCM.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IRepository{TEntity}"/>.
/// The DbContext is injected (scoped per request); no SaveChanges here.
/// </summary>
public class Repository<TEntity>(PcPartsContext context) : IRepository<TEntity> where TEntity : class
{
    protected PcPartsContext Context { get; } = context;

    protected DbSet<TEntity> DbSet { get; } = context.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await DbSet.FindAsync([id], cancellationToken);

    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        DbSet.AnyAsync(predicate, cancellationToken);

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await DbSet.AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbSet.Update(entity);
    }

    public void Remove(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbSet.Remove(entity);
    }
}
