using Microsoft.EntityFrameworkCore.Storage;
using PCM.DomainEntities.Context;
using PCM.Repositories.IRepository;
using PCM.Repositories.Repository;

namespace PCM.Repositories;

/// <summary>
/// EF Core Unit of Work. Repositories are created lazily on first use and all
/// share the same scoped <see cref="PcPartsContext"/>.
/// </summary>
public sealed class UnitOfWork(PcPartsContext context) : IUnitOfWork
{
    private IBrandRepository? _brands;

    public IBrandRepository Brands => _brands ??= new BrandRepository(context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return new EfTransaction(transaction);
    }

    private sealed class EfTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) =>
            transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
