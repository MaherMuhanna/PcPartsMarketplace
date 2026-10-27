using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PCM.Infrastructure.Services;

namespace PCM.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers business services and all FluentValidation validators in this assembly.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IBrandService, BrandService>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);

        return services;
    }
}
