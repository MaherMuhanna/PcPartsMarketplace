using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.Interceptors;

namespace PCM.Repositories;

public static class DependencyInjection
{
    /// <summary>
    /// Registers PcPartsContext (scoped, with the audit interceptor) and the Unit of Work.
    /// The connection string comes from configuration (user-secrets / env vars), never from code.
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditSaveChangesInterceptor>();

        services.AddDbContext<PcPartsContext>((serviceProvider, options) => options
            .UseSqlServer(connectionString)
            .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>())
            // By design, history rows (OrderItem, StockMovement, SellerOrder, ...) keep pointing at
            // soft-deleted principals (Offer, SellerProfile, Address). EF warns that an Include of a
            // filtered required principal can drop such rows. Queries that read history must project
            // or call IgnoreQueryFilters([PcPartsContext.SoftDeleteFilter]) on the principal; the
            // warning is acknowledged here so it does not flood the logs on every startup.
            .ConfigureWarnings(warnings => warnings.Ignore(
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
