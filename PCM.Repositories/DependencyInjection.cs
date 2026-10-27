using Microsoft.EntityFrameworkCore;
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
            .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
