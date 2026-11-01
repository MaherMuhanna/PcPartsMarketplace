using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using PCM.API.Caching;
using PCM.Repositories;

namespace PCM.ArchitectureTests;

/// <summary>Conventions every API controller follows.</summary>
public sealed class ControllerRulesTests
{
    private static readonly Type[] Controllers = typeof(CachePolicies).Assembly.GetTypes()
        .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
        .OrderBy(type => type.Name)
        .ToArray();

    [Fact]
    public void Controllers_are_found() => Assert.True(Controllers.Length >= 15, $"Only {Controllers.Length} controllers found.");

    [Fact]
    public void Controllers_are_sealed_api_controllers_under_api_v1()
    {
        foreach (var controller in Controllers)
        {
            Assert.True(controller.IsSealed, $"{controller.Name} should be sealed.");
            Assert.NotNull(controller.GetCustomAttribute<ApiControllerAttribute>());

            var route = controller.GetCustomAttribute<RouteAttribute>();
            Assert.True(route?.Template.StartsWith("api/v1/", StringComparison.Ordinal) == true,
                $"{controller.Name} route should start with api/v1/ (was '{route?.Template}').");
        }
    }

    [Fact]
    public void Controllers_are_thin_they_depend_on_services_only()
    {
        // Business logic and data access live in Infrastructure services; a controller never sees
        // a DbContext, the unit of work or a repository.
        foreach (var controller in Controllers)
        {
            var parameters = controller.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType);
            foreach (var type in parameters)
            {
                Assert.False(typeof(DbContext).IsAssignableFrom(type), $"{controller.Name} takes a DbContext ({type.Name}).");
                Assert.False(type.Assembly == typeof(IUnitOfWork).Assembly, $"{controller.Name} takes {type.Name} from PCM.Repositories.");
            }
        }
    }

    [Fact]
    public void Cached_endpoints_are_public_and_read_only()
    {
        // The catalog cache policy ignores the Authorization header (see PublicCatalogCachePolicy),
        // so a cached endpoint must never depend on who is calling.
        var cached = Controllers.Where(c => c.GetCustomAttribute<OutputCacheAttribute>() is not null
            || Actions(c).Any(a => a.GetCustomAttribute<OutputCacheAttribute>() is not null)).ToList();

        Assert.NotEmpty(cached);
        foreach (var controller in cached)
        {
            Assert.Null(controller.GetCustomAttribute<AuthorizeAttribute>());
            foreach (var action in Actions(controller))
            {
                Assert.Null(action.GetCustomAttribute<AuthorizeAttribute>());
                Assert.All(action.GetCustomAttributes<HttpMethodAttribute>(), http => Assert.Equal(new[] { "GET" }, http.HttpMethods));
            }
        }
    }

    [Theory]
    [InlineData("AdminCategoriesController", CatalogCacheScope.All)]
    [InlineData("AdminSpecOptionsController", CatalogCacheScope.All)]
    [InlineData("AdminBrandsController", CatalogCacheScope.All)]
    [InlineData("AdminProductsController", CatalogCacheScope.Products)]
    [InlineData("AdminSellersController", CatalogCacheScope.Products)]
    [InlineData("SellerOffersController", CatalogCacheScope.Products)]
    [InlineData("OrdersController", CatalogCacheScope.Products)]
    [InlineData("SellerOrdersController", CatalogCacheScope.Products)]
    [InlineData("AdminOrdersController", CatalogCacheScope.Products)]
    public void Writes_that_change_the_catalog_evict_the_cache(string controllerName, CatalogCacheScope scope)
    {
        var controller = Assert.Single(Controllers, c => c.Name == controllerName);

        Assert.Equal(scope, controller.GetCustomAttribute<InvalidatesCatalogCacheAttribute>()?.Scope);
    }

    private static IEnumerable<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
}
