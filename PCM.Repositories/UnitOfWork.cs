using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.Repositories.IRepository;
using PCM.Repositories.Repository;

namespace PCM.Repositories;

/// <summary>
/// EF Core Unit of Work. Repositories are created lazily on first use and all
/// share the same scoped <see cref="PcPartsContext"/>.
/// </summary>
/// <remarks>
/// <see cref="AuthDbContext"/> (Identity) and <see cref="PcPartsContext"/> use the
/// same scoped SqlConnection (see <see cref="DependencyInjection.AddDataAccess"/>).
/// A transaction started here is enlisted in both contexts, so a business operation
/// such as "create user + seller profile" commits or rolls back as one unit.
/// </remarks>
public sealed class UnitOfWork(PcPartsContext context, AuthDbContext authContext) : IUnitOfWork
{
    private IBrandRepository? _brands;
    private ISellerProfileRepository? _sellerProfiles;
    private IAddressRepository? _addresses;
    private IRefreshTokenRepository? _refreshTokens;
    private ICategoryRepository? _categories;
    private ISpecAttributeRepository? _specAttributes;
    private IRepository<SpecOption>? _specOptions;
    private IProductRepository? _products;
    private IOfferRepository? _offers;
    private IStockMovementRepository? _stockMovements;
    private ICartRepository? _carts;
    private IOrderRepository? _orders;
    private ISellerOrderRepository? _sellerOrders;
    private IDashboardRepository? _dashboards;

    public IBrandRepository Brands => _brands ??= new BrandRepository(context);

    public ISellerProfileRepository SellerProfiles => _sellerProfiles ??= new SellerProfileRepository(context);

    public IAddressRepository Addresses => _addresses ??= new AddressRepository(context);

    public IRefreshTokenRepository RefreshTokens => _refreshTokens ??= new RefreshTokenRepository(context);

    public ICategoryRepository Categories => _categories ??= new CategoryRepository(context);

    public ISpecAttributeRepository SpecAttributes => _specAttributes ??= new SpecAttributeRepository(context);

    public IRepository<SpecOption> SpecOptions => _specOptions ??= new Repository<SpecOption>(context);

    public IProductRepository Products => _products ??= new ProductRepository(context);

    public IOfferRepository Offers => _offers ??= new OfferRepository(context);

    public IStockMovementRepository StockMovements => _stockMovements ??= new StockMovementRepository(context);

    public ICartRepository Carts => _carts ??= new CartRepository(context);

    public IOrderRepository Orders => _orders ??= new OrderRepository(context);

    public ISellerOrderRepository SellerOrders => _sellerOrders ??= new SellerOrderRepository(context);

    public IDashboardRepository Dashboards => _dashboards ??= new DashboardRepository(context);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public void ClearChanges() => context.ChangeTracker.Clear();

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Enlist the Identity context in the same database transaction.
        await authContext.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);

        return new EfTransaction(transaction, authContext);
    }

    private sealed class EfTransaction(IDbContextTransaction transaction, AuthDbContext authContext) : IUnitOfWorkTransaction
    {
        private bool _released;

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await transaction.CommitAsync(cancellationToken);
            await ReleaseAuthContextAsync();
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await transaction.RollbackAsync(cancellationToken);
            await ReleaseAuthContextAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await ReleaseAuthContextAsync();
            await transaction.DisposeAsync();
        }

        /// <summary>
        /// Detaches the Identity context from the finished transaction, so later
        /// UserManager calls in the same request run outside it.
        /// </summary>
        private async Task ReleaseAuthContextAsync()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            await authContext.Database.UseTransactionAsync(null);
        }
    }
}
