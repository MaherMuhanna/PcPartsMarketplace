using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.Catalog;
using PCM.Infrastructure.Seeding;
using PCM.Infrastructure.Services;

namespace PCM.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers ASP.NET Core Identity (user/role managers over AuthDbContext),
    /// business services and all FluentValidation validators in this assembly.
    /// The host must also register an <see cref="Security.IAccessTokenFactory"/>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Same rules as the FluentValidation password rule (CommonRules.StrongPassword).
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;

                // 5 failed logins in a row lock the account for 15 minutes.
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;

                // The user name is the email, already validated by FluentValidation.
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty;

                // Matches Database/schema.sql: schema version 2 (no passkeys table),
                // and 128-character key columns in AspNetUserLogins / AspNetUserTokens.
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
                options.Stores.MaxLengthForKeys = 128;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<AuthDbContext>();

        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ISpecAttributeService, SpecAttributeService>();
        services.AddScoped<IProductCatalogService, ProductCatalogService>();
        services.AddScoped<IProductAdminService, ProductAdminService>();
        services.AddScoped<ISellerProposalService, SellerProposalService>();
        services.AddScoped<ProductEditor>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<ISellerAdminService, SellerAdminService>();
        services.AddScoped<ISellerProfileService, SellerProfileService>();
        services.AddScoped<DemoUserSeeder>();
        services.AddScoped<DemoCatalogSeeder>();

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, ServiceLifetime.Singleton);

        return services;
    }
}
