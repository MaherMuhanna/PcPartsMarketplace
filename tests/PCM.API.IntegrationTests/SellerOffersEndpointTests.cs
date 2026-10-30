using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.DomainEntities.Context;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Offers;

namespace PCM.API.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class SellerOffersEndpointTests(ApiFactory factory)
{
    private const string BasePath = "/api/v1/seller/offers";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Admin, a throwaway category with one published product, and an approved seller.</summary>
    private async Task<(HttpClient Admin, HttpClient Seller, ProductManageDto Product, TestCategory Test)> ArrangeAsync()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var product = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Alpha, 16));
        var seller = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));
        return (admin, seller, product, test);
    }

    private static CreateOfferRequest NewOffer(int productId, string condition = "New", decimal price = 120.50m, int stock = 10) =>
        new() { ProductId = productId, Condition = condition, Price = price, Stock = stock };

    [Fact]
    public async Task Offer_lifecycle_create_stock_update_delete()
    {
        var (_, seller, product, _) = await ArrangeAsync();
        var anonymous = factory.CreateClient();

        // 1. Create with initial stock: 201 + Location, history starts with a Restock.
        var response = await seller.PostAsJsonAsync(BasePath, NewOffer(product.Id), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var offer = (await response.Content.ReadFromJsonAsync<OfferDto>(Ct))!;
        Assert.Equal((10, 0, 10, 5, false, true), (offer.Stock, offer.ReservedStock, offer.Available, offer.LowStockThreshold, offer.IsLowStock, offer.ProductIsPublished));

        var history = await seller.GetFromJsonAsync<PagedResponse<StockMovementDto>>($"{BasePath}/{offer.Id}/movements", Ct);
        var initial = Assert.Single(history!.Items);
        Assert.Equal(("Restock", 10, 10, "Initial stock"), (initial.Reason, initial.Change, initial.StockAfter, initial.Note));

        // 2. The public catalog shows it at once: product page offer and search price.
        var page = await anonymous.GetFromJsonAsync<ProductDetailsDto>($"/api/v1/products/by-slug/{product.Slug}", Ct);
        var shown = Assert.Single(page!.Offers);
        Assert.Equal((offer.Id, 120.50m, 10), (shown.OfferId, shown.Price, shown.Available));

        var search = await anonymous.GetFromJsonAsync<PagedResponse<ProductListItemDto>>($"/api/v1/products?q={Uri.EscapeDataString(product.Name)}", Ct);
        var card = Assert.Single(search!.Items);
        Assert.Equal(((decimal?)120.50m, true, 1), (card.MinPrice, card.InStock, card.OfferCount));

        // 3. Adjustment down to 3: now low stock (threshold 5).
        var adjusted = await PatchStockAsync(seller, offer.Id, new StockChangeRequest { Type = "Adjustment", Change = -7, Note = "Stock count" });
        Assert.Equal((3, 3, true), (adjusted.Stock, adjusted.Available, adjusted.IsLowStock));

        var lowStock = await seller.GetFromJsonAsync<PagedResponse<OfferDto>>($"{BasePath}/low-stock", Ct);
        Assert.Contains(lowStock!.Items, o => o.Id == offer.Id);
        var inTab = await seller.GetFromJsonAsync<PagedResponse<OfferDto>>($"{BasePath}?stock=low&q={Uri.EscapeDataString(product.ModelNumber)}", Ct);
        Assert.Equal(offer.Id, Assert.Single(inTab!.Items).Id);

        // 4. Price and threshold change; stock is untouched.
        var updated = await TestCatalog.PutAsync<OfferDto>(seller, $"{BasePath}/{offer.Id}", new UpdateOfferRequest { Price = 99.99m, LowStockThreshold = 2 });
        Assert.Equal((99.99m, 2, 3, false), (updated.Price, updated.LowStockThreshold, updated.Stock, updated.IsLowStock));

        history = await seller.GetFromJsonAsync<PagedResponse<StockMovementDto>>($"{BasePath}/{offer.Id}/movements", Ct);
        Assert.Equal(new[] { "Adjustment", "Restock" }, history!.Items.Select(m => m.Reason));   // newest first

        // 5. Delete: gone for the seller and the catalog; the same product + condition can be offered again.
        Assert.Equal(HttpStatusCode.NoContent, (await seller.DeleteAsync($"{BasePath}/{offer.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync($"{BasePath}/{offer.Id}", Ct)).StatusCode);
        page = await anonymous.GetFromJsonAsync<ProductDetailsDto>($"/api/v1/products/by-slug/{product.Slug}", Ct);
        Assert.Empty(page!.Offers);

        Assert.Equal(HttpStatusCode.Created, (await seller.PostAsJsonAsync(BasePath, NewOffer(product.Id, stock: 0), Ct)).StatusCode);
    }

    [Fact]
    public async Task One_offer_per_product_and_condition()
    {
        var (_, seller, product, _) = await ArrangeAsync();

        await TestCatalog.PostAsync<OfferDto>(seller, BasePath, NewOffer(product.Id, "New"));

        Assert.Equal(HttpStatusCode.Conflict, (await seller.PostAsJsonAsync(BasePath, NewOffer(product.Id, "new"), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await seller.PostAsJsonAsync(BasePath, NewOffer(product.Id, "Used", price: 80m), Ct)).StatusCode);
    }

    [Fact]
    public async Task Only_published_products_can_be_offered()
    {
        var (admin, seller, product, test) = await ArrangeAsync();

        // Pending proposal: not in the catalog yet.
        var proposal = await TestCatalog.PostAsync<ProductManageDto>(seller, "/api/v1/seller/products/proposals", test.NewProduct(test.Beta, 8));
        await AssertValidationErrorAsync(await seller.PostAsJsonAsync(BasePath, NewOffer(proposal.Id), Ct), "productId");
        await AssertValidationErrorAsync(await seller.PostAsJsonAsync(BasePath, NewOffer(999_999), Ct), "productId");

        // Deactivated after the offer exists: the offer stays for the seller, flagged, and leaves the catalog.
        var offer = await TestCatalog.PostAsync<OfferDto>(seller, BasePath, NewOffer(product.Id));
        await TestCatalog.PostAsync<ProductManageDto>(admin, $"/api/v1/admin/products/{product.Id}/deactivate", body: null);

        var mine = await seller.GetFromJsonAsync<OfferDto>($"{BasePath}/{offer.Id}", Ct);
        Assert.False(mine!.ProductIsPublished);
        await AssertValidationErrorAsync(await seller.PostAsJsonAsync(BasePath, NewOffer(product.Id, "Used"), Ct), "productId");
    }

    [Fact]
    public async Task Invalid_input_returns_400_with_field_errors()
    {
        var (_, seller, product, _) = await ArrangeAsync();
        var offer = await TestCatalog.PostAsync<OfferDto>(seller, BasePath, NewOffer(product.Id));

        await AssertValidationErrorAsync(await seller.PostAsJsonAsync(BasePath, NewOffer(product.Id, price: 10.555m), Ct), "price");
        await AssertValidationErrorAsync(await seller.PostAsJsonAsync(BasePath, NewOffer(product.Id, condition: "Refurbished"), Ct), "condition");
        await AssertValidationErrorAsync(await seller.PatchAsJsonAsync($"{BasePath}/{offer.Id}/stock",
            new StockChangeRequest { Type = "Restock", Change = -1 }, Ct), "change");
        await AssertValidationErrorAsync(await seller.PatchAsJsonAsync($"{BasePath}/{offer.Id}/stock",
            new StockChangeRequest { Type = "Adjustment", Change = -1 }, Ct), "note");
        await AssertValidationErrorAsync(await seller.PatchAsJsonAsync($"{BasePath}/{offer.Id}/stock",
            new StockChangeRequest { Type = "Sale", Change = -1, Note = "Not allowed" }, Ct), "type");
        await AssertValidationErrorAsync(await seller.GetAsync($"{BasePath}?stock=everything", Ct), "stock");

        // Removing more than is in stock is a conflict, not a negative stock.
        var tooMuch = await seller.PatchAsJsonAsync($"{BasePath}/{offer.Id}/stock",
            new StockChangeRequest { Type = "Adjustment", Change = -11, Note = "Count" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, tooMuch.StatusCode);
    }

    [Fact]
    public async Task Reserved_stock_cannot_be_removed_or_deleted()
    {
        var (_, seller, product, _) = await ArrangeAsync();
        var offer = await TestCatalog.PostAsync<OfferDto>(seller, BasePath, NewOffer(product.Id, stock: 10));

        // Orders arrive in Phase 5; simulate 4 units reserved by an open order.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PcPartsContext>();
            await db.Offers.Where(o => o.Id == offer.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.ReservedStock, 4), Ct);
        }

        var below = await seller.PatchAsJsonAsync($"{BasePath}/{offer.Id}/stock",
            new StockChangeRequest { Type = "Adjustment", Change = -7, Note = "Count" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, below.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await seller.DeleteAsync($"{BasePath}/{offer.Id}", Ct)).StatusCode);

        // Down to exactly the reserved quantity is fine: 0 available, out of stock, low.
        var atReserved = await PatchStockAsync(seller, offer.Id, new StockChangeRequest { Type = "Adjustment", Change = -6, Note = "Count" });
        Assert.Equal((4, 4, 0, true), (atReserved.Stock, atReserved.ReservedStock, atReserved.Available, atReserved.IsLowStock));

        var outOfStock = await seller.GetFromJsonAsync<PagedResponse<OfferDto>>($"{BasePath}?stock=out", Ct);
        Assert.Contains(outOfStock!.Items, o => o.Id == offer.Id);

        var movement = (await seller.GetFromJsonAsync<PagedResponse<StockMovementDto>>($"{BasePath}/{offer.Id}/movements", Ct))!.Items[0];
        Assert.Equal((4, 4), (movement.StockAfter, movement.ReservedStockAfter));
    }

    [Fact]
    public async Task Other_sellers_offers_return_404()
    {
        var (admin, owner, product, _) = await ArrangeAsync();
        var other = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));
        var offer = await TestCatalog.PostAsync<OfferDto>(owner, BasePath, NewOffer(product.Id));
        var path = $"{BasePath}/{offer.Id}";

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync(path, new UpdateOfferRequest { Price = 1m, LowStockThreshold = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync($"{path}/stock", new StockChangeRequest { Type = "Restock", Change = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/movements", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync(path, Ct)).StatusCode);

        var othersList = await other.GetFromJsonAsync<PagedResponse<OfferDto>>(BasePath, Ct);
        Assert.DoesNotContain(othersList!.Items, o => o.Id == offer.Id);

        // The owner's offer is untouched.
        var mine = await owner.GetFromJsonAsync<OfferDto>(path, Ct);
        Assert.Equal((120.50m, 10), (mine!.Price, mine.Stock));
    }

    [Fact]
    public async Task Only_approved_sellers_can_sell()
    {
        var pendingSeller = factory.CreateClient(await factory.RegisterSellerAsync());
        var customer = factory.CreateClient(await factory.RegisterCustomerAsync());
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Forbidden, (await pendingSeller.GetAsync(BasePath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pendingSeller.PostAsJsonAsync(BasePath, NewOffer(1), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(BasePath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(BasePath, Ct)).StatusCode);
    }

    // ---------------------------------------------------------------------

    private static async Task<OfferDto> PatchStockAsync(HttpClient client, int offerId, StockChangeRequest request)
    {
        var response = await client.PatchAsJsonAsync($"{BasePath}/{offerId}/stock", request, Ct);
        Assert.True(response.IsSuccessStatusCode, $"PATCH stock returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<OfferDto>(Ct))!;
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Expected 400 for '{field}' but got {(int)response.StatusCode}: {body}");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.True(problem!.Errors.ContainsKey(field), $"Expected an error for '{field}': {body}");
    }
}
