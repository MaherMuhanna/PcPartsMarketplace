using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Common;

namespace PCM.DomainEntities.Context;

/// <summary>
/// Hand-written half of the scaffolded <see cref="PcPartsContext"/>.
/// Re-scaffolding regenerates PcPartsContext.cs but never touches this file.
/// </summary>
public partial class PcPartsContext
{
    /// <summary>Name of the global soft-delete filter (EF Core 10 named query filters).</summary>
    public const string SoftDeleteFilter = "SoftDelete";

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // Hide soft-deleted rows from every query on every ISoftDeletable entity.
        // Bypass for one query with: .IgnoreQueryFilters([PcPartsContext.SoftDeleteFilter])
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // Builds: (TEntity e) => !e.IsDeleted
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(isDeleted), parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(SoftDeleteFilter, filter);
        }
    }
}
