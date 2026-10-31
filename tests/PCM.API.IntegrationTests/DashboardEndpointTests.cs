using System.Net;
using System.Net.Http.Json;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Dashboards;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.API.IntegrationTests;

/// <summary>
/// Admin and seller dashboards. Tests of one collection run one after another, so the admin numbers
/// are checked as differences (before / after); a new seller's numbers are exact.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class DashboardEndpointTests(ApiFactory factory)
{
    private const string AdminPath = "/api/v1/admin/dashboard";
    private const string SellerPath = "/api/v1/seller/dashboard";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dashboards_count_reviews_orders_stock_and_delivered_sales()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var before = await GetAsync<AdminDashboardDto>(admin, AdminPath);

        // A seller sign-up and a product proposal wait for review.
        await factory.RegisterSellerAsync();
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var seller = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));
        await TestCatalog.PostAsync<ProductManageDto>(seller, "/api/v1/seller/products/proposals", test.NewProduct(test.Alpha, 8));

        // Three offers: plenty (20), low (3 <= threshold 5) and out of stock (0, also low).
        var p1 = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Alpha, 16));
        var p2 = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Beta, 32));
        var p3 = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Beta, 64));
        var plenty = await CreateOfferAsync(seller, p1.Id, 50m, 20);
        await CreateOfferAsync(seller, p2.Id, 30m, 3);
        await CreateOfferAsync(seller, p3.Id, 10m, 0);

        // Two orders: the first is delivered (2 x 50), the second stays Pending (1 x 50).
        var customer = factory.CreateClient(await factory.RegisterCustomerAsync());
        var address = await TestCatalog.PostAsync<AddressDto>(customer, "/api/v1/account/addresses",
            new CreateAddressRequest { City = "Amman", Street = "Dashboard Street 1", Phone = "+962790000002", IsDefault = true });

        var delivered = await CheckoutAsync(customer, plenty.Id, 2, address.Id);
        var subOrderId = Assert.Single(delivered.SellerOrders).Id;
        foreach (var status in new[] { "Confirmed", "Shipped", "Delivered" })
        {
            var response = await seller.PatchAsJsonAsync($"/api/v1/seller/orders/{subOrderId}/status",
                new ChangeSellerOrderStatusRequest { Status = status }, Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var pending = await CheckoutAsync(customer, plenty.Id, 1, address.Id);

        // Seller numbers are exact (new seller).
        var mine = await GetAsync<SellerDashboardDto>(seller, SellerPath);
        Assert.Equal(new SubOrderStatusCountsDto(1, 0, 0, 1, 0), mine.SubOrdersByStatus);
        Assert.Equal((3, 2, 1), (mine.OfferCount, mine.LowStockCount, mine.OutOfStockCount));
        Assert.Equal((100m, 1), (mine.DeliveredSalesLast30Days, mine.DeliveredOrdersLast30Days));
        Assert.Equal(
            new[] { (pending.OrderNumber, "Pending"), (delivered.OrderNumber, "Delivered") },
            mine.RecentSubOrders.Select(r => (r.OrderNumber, r.Status)));   // newest first

        // The low-stock card and the low-stock list use the same rule.
        var lowStock = await GetAsync<PagedResponse<OfferDto>>(seller, "/api/v1/seller/offers/low-stock");
        Assert.Equal(mine.LowStockCount, lowStock.TotalCount);

        // Admin numbers grew by exactly what this test did.
        var after = await GetAsync<AdminDashboardDto>(admin, AdminPath);
        Assert.Equal(before.PendingSellers + 1, after.PendingSellers);
        Assert.Equal(before.PendingProposals + 1, after.PendingProposals);
        Assert.Equal(before.OrdersLast7Days + 2, after.OrdersLast7Days);
        Assert.Equal(before.SubOrdersByStatus.Pending + 1, after.SubOrdersByStatus.Pending);
        Assert.Equal(before.SubOrdersByStatus.Delivered + 1, after.SubOrdersByStatus.Delivered);
        Assert.Equal(before.DeliveredSalesLast30Days + 100m, after.DeliveredSalesLast30Days);
    }

    [Fact]
    public async Task Dashboards_are_limited_to_their_roles()
    {
        var anonymous = factory.CreateClient();
        var customer = factory.CreateClient(await factory.RegisterCustomerAsync());
        var pendingSeller = factory.CreateClient(await factory.RegisterSellerAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(AdminPath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(SellerPath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(AdminPath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(SellerPath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pendingSeller.GetAsync(AdminPath, Ct)).StatusCode);

        // Only approved sellers have a dashboard; the UI shows the store status instead.
        Assert.Equal(HttpStatusCode.Forbidden, (await pendingSeller.GetAsync(SellerPath, Ct)).StatusCode);
    }

    // ---------------------------------------------------------------------

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        Assert.True(response.IsSuccessStatusCode, $"GET {url} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    private static Task<OfferDto> CreateOfferAsync(HttpClient seller, int productId, decimal price, int stock) =>
        TestCatalog.PostAsync<OfferDto>(seller, "/api/v1/seller/offers",
            new CreateOfferRequest { ProductId = productId, Condition = "New", Price = price, Stock = stock });

    /// <summary>Puts one line in the cart and checks out exactly what the cart shows.</summary>
    private static async Task<OrderDto> CheckoutAsync(HttpClient customer, int offerId, int quantity, int addressId)
    {
        var cart = await TestCatalog.PostAsync<CartDto>(customer, "/api/v1/cart/items",
            new AddCartItemRequest { OfferId = offerId, Quantity = quantity });

        return await TestCatalog.PostAsync<OrderDto>(customer, "/api/v1/orders/checkout", new CheckoutRequest
        {
            AddressId = addressId,
            Items = cart.Sellers.SelectMany(s => s.Items)
                .Select(i => new CheckoutItemRequest { CartItemId = i.Id, UnitPrice = i.UnitPrice, Quantity = i.Quantity })
                .ToList()
        });
    }
}
