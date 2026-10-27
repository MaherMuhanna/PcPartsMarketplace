using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PCM.DomainEntities.Common;

namespace PCM.DomainEntities.Interceptors;

/// <summary>
/// Applies the audit and soft-delete conventions on every SaveChanges, so no
/// service or repository has to remember them:
/// <list type="bullet">
///   <item>Added <see cref="IAuditable"/>: CreatedAt = now (UTC).</item>
///   <item>Modified <see cref="IAuditable"/>: UpdatedAt = now (UTC); CreatedAt is never overwritten.</item>
///   <item>Deleted <see cref="ISoftDeletable"/>: turned into an update that sets IsDeleted = true.</item>
/// </list>
/// </summary>
public sealed class AuditSaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyConventions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyConventions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyConventions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            // Soft delete first: a deleted entity becomes a modified one.
            if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable softDeletable })
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
            }

            if (entry.Entity is not IAuditable auditable)
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    auditable.CreatedAt = now;
                    auditable.UpdatedAt = null;
                    break;

                case EntityState.Modified:
                    auditable.UpdatedAt = now;
                    // Protect the original creation time from accidental overwrites.
                    entry.Property(nameof(IAuditable.CreatedAt)).IsModified = false;
                    break;
            }
        }
    }
}
