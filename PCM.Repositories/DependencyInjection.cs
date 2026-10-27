using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.Interceptors;

namespace PCM.Repositories;

public static class DependencyInjection
{
    /// <summary>
    /// Registers PcPartsContext and AuthDbContext (both scoped, with the audit
    /// interceptor) and the Unit of Work. The connection string comes from
    /// configuration (user-secrets / env vars), never from code.
    /// </summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AuditSaveChangesInterceptor>();

        // One connection per request scope, shared by both contexts so one transaction
        // can span Identity and domain tables. EF does not own (or dispose) a connection
        // it is given; the DI scope disposes it at the end of the request.
        services.AddScoped(_ => new SqlConnection(connectionString));

        services.AddDbContext<PcPartsContext>((serviceProvider, options) => options
            .UseSqlServer(serviceProvider.GetRequiredService<SqlConnection>())
            .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>())
            // By design, history rows (OrderItem, StockMovement, SellerOrder, ...) keep pointing at
            // soft-deleted principals (Offer, SellerProfile, Address). EF warns that an Include of a
            // filtered required principal can drop such rows. Queries that read history must project
            // or call IgnoreQueryFilters([PcPartsContext.SoftDeleteFilter]) on the principal; the
            // warning is acknowledged here so it does not flood the logs on every startup.
            .ConfigureWarnings(warnings => warnings.Ignore(
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

        services.AddDbContext<AuthDbContext>((serviceProvider, options) => options
            .UseSqlServer(serviceProvider.GetRequiredService<SqlConnection>())
            .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
