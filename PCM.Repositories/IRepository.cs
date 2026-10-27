using System.Linq.Expressions;

namespace PCM.Repositories;

/// <summary>
/// Generic repository with the operations every entity needs.
/// Repositories only stage changes in the DbContext; they never call SaveChanges.
/// Persisting is the job of <see cref="IUnitOfWork.SaveChangesAsync"/>, so one
/// business operation commits once, atomically.
/// </summary>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>Finds a tracked entity by its int primary key, or null. Soft-deleted rows are excluded.</summary>
    Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>True if any (non-deleted) entity matches the predicate.</summary>
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>Stages a new entity for insert.</summary>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Stages an entity for update (only needed for detached entities).</summary>
    void Update(TEntity entity);

    /// <summary>
    /// Stages an entity for removal. For soft-deletable entities the audit
    /// interceptor turns this into IsDeleted = true instead of a DELETE.
    /// </summary>
    void Remove(TEntity entity);
}
