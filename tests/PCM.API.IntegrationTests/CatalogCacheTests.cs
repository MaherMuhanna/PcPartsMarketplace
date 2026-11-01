using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.DomainEntities.Context;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Offers;

namespace PCM.API.IntegrationTests;

/// <summary>
/// Output cache on catalog reads. A change made directly in the database (behind the API's back)
/// proves a response came from the cache; a change through the API proves the write evicted it.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class CatalogCacheTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Product_page_is_cached_for_everyone_until_a_successful_write_evicts_it()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var product = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Alpha, 16));
        var seller = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));
        var offer = await TestCatalog.PostAsync<OfferDto>(seller, "/api/v1/seller/offers",
            new CreateOfferRequest { ProductId = product.Id, Condition = "New", Price = 100m, Stock = 5 });

        var anonymous = factory.CreateClient();
        var customer = factory.CreateClient(await factory.RegisterCustomerAsync());
        var path = $"/api/v1/products/{product.Id}";

        Assert.Equal(100m, await OfferPriceAsync(anonymous, path));   // first read fills the cache

        await SetPriceInDatabaseAsync(offer.Id, 77m);

        // Served from the cache: the database change is not visible yet, also not to a signed-in
        // client (Authorization header), which the default output-cache policy would never cache.
        Assert.Equal(100m, await OfferPriceAsync(anonymous, path));
        Assert.Equal(100m, await OfferPriceAsync(customer, path));

        // A rejected write (400) evicts nothing.
        var invalid = await seller.PutAsJsonAsync($"/api/v1/seller/offers/{offer.Id}",
            new UpdateOfferRequest { Price = 0m, LowStockThreshold = 5 }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(100m, await OfferPriceAsync(anonymous, path));

        // A successful write evicts the product pages: the next read is fresh.
        await TestCatalog.PutAsync<OfferDto>(seller, $"/api/v1/seller/offers/{offer.Id}",
            new UpdateOfferRequest { Price = 90m, LowStockThreshold = 5 });
        Assert.Equal(90m, await OfferPriceAsync(anonymous, path));
        Assert.Equal(90m, await OfferPriceAsync(customer, path));
    }

    [Fact]
    public async Task Each_query_string_is_its_own_cache_entry()
    {
        var anonymous = factory.CreateClient();

        var ram = await anonymous.GetFromJsonAsync<PagedResponse<ProductListItemDto>>("/api/v1/products?category=ram&pageSize=100", Ct);
        var ddr4 = await anonymous.GetFromJsonAsync<PagedResponse<ProductListItemDto>>("/api/v1/products?category=ram&memory-type=ddr4&pageSize=100", Ct);

        Assert.Equal((6, 1), (ram!.TotalCount, ddr4!.TotalCount));
    }

    [Fact]
    public async Task Catalog_edits_evict_the_category_tree_and_brands()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var anonymous = factory.CreateClient();

        // Fill both caches first.
        await anonymous.GetFromJsonAsync<List<CategoryTreeDto>>("/api/v1/categories", Ct);
        await anonymous.GetFromJsonAsync<PagedResponse<BrandDto>>("/api/v1/brands?pageSize=100&sort=newest", Ct);

        var test = await TestCatalog.CreateCategoryAsync(admin);   // creates a category and a brand

        var tree = await anonymous.GetFromJsonAsync<List<CategoryTreeDto>>("/api/v1/categories", Ct);
        Assert.Contains(tree!, c => c.Id == test.Category.Id);

        var brands = await anonymous.GetFromJsonAsync<PagedResponse<BrandDto>>("/api/v1/brands?pageSize=100&sort=newest", Ct);
        Assert.Contains(brands!.Items, b => b.Id == test.Brand.Id);
    }

    private static async Task<decimal> OfferPriceAsync(HttpClient client, string path)
    {
        var page = await client.GetFromJsonAsync<ProductDetailsDto>(path, Ct);
        return Assert.Single(page!.Offers).Price;
    }

    private async Task SetPriceInDatabaseAsync(int offerId, decimal price)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PcPartsContext>();
        await db.Offers.Where(o => o.Id == offerId).ExecuteUpdateAsync(s => s.SetProperty(o => o.Price, price), Ct);
    }
}
