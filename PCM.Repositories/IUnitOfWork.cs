using PCM.DomainEntities.DBEntities;
using PCM.Repositories.IRepository;

namespace PCM.Repositories;

/// <summary>
/// Single entry point to the repositories of one request, sharing one DbContext.
/// Business operations stage changes through the repositories, then commit once
/// with <see cref="SaveChangesAsync"/>. Multi-step operations that must succeed or
/// fail together (e.g. checkout) wrap the work in <see cref="BeginTransactionAsync"/>.
/// </summary>
public interface IUnitOfWork
{
    IBrandRepository Brands { get; }

    ISellerProfileRepository SellerProfiles { get; }

    IAddressRepository Addresses { get; }

    IRefreshTokenRepository RefreshTokens { get; }

    ICategoryRepository Categories { get; }

    ISpecAttributeRepository SpecAttributes { get; }

    IRepository<SpecOption> SpecOptions { get; }

    IProductRepository Products { get; }

    /// <summary>Persists all staged changes in one round trip; returns rows affected.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a database transaction. Dispose without commit = rollback.
    /// The transaction also covers ASP.NET Core Identity (UserManager) writes,
    /// because both DbContexts share the request's connection.
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>A database transaction that does not leak EF Core types to callers.</summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
