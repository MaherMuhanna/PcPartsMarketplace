using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.DomainEntities.Context;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Orders;

namespace PCM.API.IntegrationTests;

/// <summary>
/// Cart, checkout and the sub-order status flow through the real API and database.
/// Every test builds its own products (in a throwaway category), sellers and customer, so the seeded
/// catalog counts stay exact and tests never share stock.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class CheckoutAndOrdersEndpointTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Two products; seller A offers P1 (100, stock 5) and P2 (40, stock 10); seller B offers P1 (90, stock 3).
    /// The customer has one default address.
    /// </summary>
    private sealed record Market(
        HttpClient Admin,
        HttpClient SellerA,
        HttpClient SellerB,
        HttpClient Customer,
        OfferDto A1,
        OfferDto A2,
        OfferDto B1,
        AddressDto Address);

    private async Task<Market> ArrangeAsync()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var p1 = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Alpha, 16));
        var p2 = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Beta, 32));

        var sellerA = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));
        var sellerB = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));

        var a1 = await CreateOfferAsync(sellerA, p1.Id, 100m, 5);
        var a2 = await CreateOfferAsync(sellerA, p2.Id, 40m, 10);
        var b1 = await CreateOfferAsync(sellerB, p1.Id, 90m, 3);

        var customer = factory.CreateClient(await factory.RegisterCustomerAsync());
        var address = await TestCatalog.PostAsync<AddressDto>(customer, "/api/v1/account/addresses",
            new CreateAddressRequest { City = "Amman", Street = "Test Street 1", Phone = "+962790000001", IsDefault = true });

        return new Market(admin, sellerA, sellerB, customer, a1, a2, b1, address);
    }

    private static Task<OfferDto> CreateOfferAsync(HttpClient seller, int productId, decimal price, int stock) =>
        TestCatalog.PostAsync<OfferDto>(seller, "/api/v1/seller/offers",
            new CreateOfferRequest { ProductId = productId, Condition = "New", Price = price, Stock = stock });

    private static Task<CartDto> AddAsync(HttpClient customer, int offerId, int quantity) =>
        TestCatalog.PostAsync<CartDto>(customer, "/api/v1/cart/items", new AddCartItemRequest { OfferId = offerId, Quantity = quantity });

    /// <summary>The checkout body a page would post: every line as the cart shows it now.</summary>
    private static CheckoutRequest AsSeen(CartDto cart, int addressId) => new()
    {
        AddressId = addressId,
        Items = cart.Sellers.SelectMany(s => s.Items)
            .Select(i => new CheckoutItemRequest { CartItemId = i.Id, UnitPrice = i.UnitPrice, Quantity = i.Quantity })
            .ToList()
    };

    private static async Task<OfferDto> GetOfferAsync(HttpClient seller, int id) =>
        (await seller.GetFromJsonAsync<OfferDto>($"/api/v1/seller/offers/{id}", Ct))!;

    private static Task<SellerOrderDetailsDto> MoveAsync(HttpClient seller, int sellerOrderId, string status, string? reason = null) =>
        PatchAsync<SellerOrderDetailsDto>(seller, $"/api/v1/seller/orders/{sellerOrderId}/status",
            new ChangeSellerOrderStatusRequest { Status = status, Reason = reason });

    private static async Task<T> PatchAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PatchAsJsonAsync(url, body, Ct);
        Assert.True(response.IsSuccessStatusCode, $"PATCH {url} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    // ---------------------------------------------------------------------

    [Fact]
    public async Task Checkout_splits_the_order_per_seller_and_the_flow_moves_stock()
    {
        var m = await ArrangeAsync();

        // 1. Cart: adding the same offer twice merges the line; lines are grouped by seller.
        await AddAsync(m.Customer, m.A1.Id, 1);
        await AddAsync(m.Customer, m.A1.Id, 1);
        await AddAsync(m.Customer, m.A2.Id, 1);
        var cart = await AddAsync(m.Customer, m.B1.Id, 1);

        Assert.Equal(2, cart.Sellers.Count);
        Assert.Equal((4, 330m, true), (cart.ItemCount, cart.Total, cart.CanCheckout));    // 2 x 100 + 40 + 90
        Assert.Equal(2, cart.Sellers.SelectMany(s => s.Items).Single(i => i.OfferId == m.A1.Id).Quantity);
        Assert.Equal(4, (await m.Customer.GetFromJsonAsync<CartSummaryDto>("/api/v1/cart/summary", Ct))!.ItemCount);

        // 2. Checkout: 201 + Location, one sub-order per seller, cash on delivery, cart emptied.
        var response = await m.Customer.PostAsJsonAsync("/api/v1/orders/checkout", AsSeen(cart, m.Address.Id), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>(Ct))!;

        Assert.StartsWith("PCM-", order.OrderNumber);
        Assert.Equal((330m, "COD", m.Address.Id), (order.Total, order.PaymentMethod, order.ShippingAddress.Id));
        Assert.Equal(2, order.SellerOrders.Count);
        Assert.All(order.SellerOrders, s => Assert.Equal("Pending", s.Status));
        Assert.Equal(order.Total, order.SellerOrders.Sum(s => s.Subtotal));
        Assert.True(order.CanCancel);

        var emptied = (await m.Customer.GetFromJsonAsync<CartDto>("/api/v1/cart", Ct))!;
        Assert.Equal(0, emptied.ItemCount);

        // 3. Stock is reserved, not sold, and the movement points at the sub-order.
        var a1 = await GetOfferAsync(m.SellerA, m.A1.Id);
        Assert.Equal((5, 2, 3), (a1.Stock, a1.ReservedStock, a1.Available));
        var subA = order.SellerOrders.Single(s => s.Items.Any(i => i.OfferId == m.A1.Id));
        var subB = order.SellerOrders.Single(s => s.Id != subA.Id);
        var reserve = (await m.SellerA.GetFromJsonAsync<PagedResponse<StockMovementDto>>($"/api/v1/seller/offers/{m.A1.Id}/movements", Ct))!.Items[0];
        Assert.Equal(("Reserve", 2, subA.Id), (reserve.Reason, reserve.Change, reserve.SellerOrderId));

        // 4. Each seller sees only their own part (another seller's id is 404).
        var listA = (await m.SellerA.GetFromJsonAsync<PagedResponse<SellerOrderListItemDto>>("/api/v1/seller/orders", Ct))!;
        Assert.Equal(subA.Id, Assert.Single(listA.Items).Id);
        Assert.Equal("Test Customer", listA.Items[0].CustomerName);
        Assert.Equal(HttpStatusCode.NotFound, (await m.SellerB.GetAsync($"/api/v1/seller/orders/{subA.Id}", Ct)).StatusCode);

        // 5. Seller A: confirm, ship, deliver. Delivery sells the reserved units.
        await MoveAsync(m.SellerA, subA.Id, "Confirmed");
        await MoveAsync(m.SellerA, subA.Id, "Shipped");
        var delivered = await MoveAsync(m.SellerA, subA.Id, "Delivered");
        Assert.Equal(("Delivered", 0), (delivered.Status, delivered.NextStatuses.Count));
        Assert.NotNull(delivered.DeliveredAt);

        a1 = await GetOfferAsync(m.SellerA, m.A1.Id);
        Assert.Equal((3, 0, 3), (a1.Stock, a1.ReservedStock, a1.Available));

        // 6. The customer cancels seller B's part: its stock is released.
        var afterCancel = await TestCatalog.PostAsync<OrderDto>(m.Customer, $"/api/v1/orders/{order.Id}/seller-orders/{subB.Id}/cancel",
            new CancelOrderRequest { Reason = "Changed my mind" });
        var cancelled = afterCancel.SellerOrders.Single(s => s.Id == subB.Id);
        Assert.Equal(("Cancelled", "Customer: Changed my mind"), (cancelled.Status, cancelled.CancelReason));
        Assert.False(afterCancel.CanCancel);

        var b1 = await GetOfferAsync(m.SellerB, m.B1.Id);
        Assert.Equal((3, 0), (b1.Stock, b1.ReservedStock));

        // 7. Nothing left to cancel: 409. The order history shows both final states.
        Assert.Equal(HttpStatusCode.Conflict, (await m.Customer.PostAsJsonAsync($"/api/v1/orders/{order.Id}/cancel", new CancelOrderRequest(), Ct)).StatusCode);

        var history = (await m.Customer.GetFromJsonAsync<PagedResponse<OrderListItemDto>>("/api/v1/orders", Ct))!;
        var row = Assert.Single(history.Items);
        Assert.Equal((order.Id, 4), (row.Id, row.ItemCount));
        Assert.Equal(new[] { "Cancelled", "Delivered" }, row.SellerOrders.Select(s => s.Status).Order());
    }

    [Fact]
    public async Task Checkout_returns_409_with_the_changed_lines()
    {
        var m = await ArrangeAsync();
        await AddAsync(m.Customer, m.A1.Id, 2);
        var cart = await AddAsync(m.Customer, m.B1.Id, 3);
        var seen = AsSeen(cart, m.Address.Id);

        // After the customer opened checkout: A raises the price, B sells out to 1 left.
        await TestCatalog.PutAsync<OfferDto>(m.SellerA, $"/api/v1/seller/offers/{m.A1.Id}", new UpdateOfferRequest { Price = 105m, LowStockThreshold = 5 });
        await PatchAsync<OfferDto>(m.SellerB, $"/api/v1/seller/offers/{m.B1.Id}/stock",
            new StockChangeRequest { Type = "Adjustment", Change = -2, Note = "Damaged in storage" });

        var response = await m.Customer.PostAsJsonAsync("/api/v1/orders/checkout", seen, Ct);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var items = problem.RootElement.GetProperty("items").EnumerateArray().ToList();
        var byOffer = items.ToDictionary(i => i.GetProperty("offerId").GetInt32(), i => i);
        Assert.Equal("PriceChanged", byOffer[m.A1.Id].GetProperty("problem").GetString());
        Assert.Equal(105m, byOffer[m.A1.Id].GetProperty("currentPrice").GetDecimal());
        Assert.Equal("InsufficientStock", byOffer[m.B1.Id].GetProperty("problem").GetString());
        Assert.Equal(1, byOffer[m.B1.Id].GetProperty("available").GetInt32());

        // Nothing happened: no reservation, the cart is untouched and shows the problem line.
        Assert.Equal(0, (await GetOfferAsync(m.SellerA, m.A1.Id)).ReservedStock);
        var current = (await m.Customer.GetFromJsonAsync<CartDto>("/api/v1/cart", Ct))!;
        Assert.Equal(5, current.ItemCount);
        Assert.False(current.CanCheckout);
        Assert.Equal(CartIssues.InsufficientStock, current.Sellers.SelectMany(s => s.Items).Single(i => i.OfferId == m.B1.Id).Issue);

        // Fix the line, review again (new price), and the checkout goes through.
        var lineB = current.Sellers.SelectMany(s => s.Items).Single(i => i.OfferId == m.B1.Id);
        current = await TestCatalog.PutAsync<CartDto>(m.Customer, $"/api/v1/cart/items/{lineB.Id}", new UpdateCartItemRequest { Quantity = 1 });
        var placed = await m.Customer.PostAsJsonAsync("/api/v1/orders/checkout", AsSeen(current, m.Address.Id), Ct);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        Assert.Equal(2 * 105m + 90m, (await placed.Content.ReadFromJsonAsync<OrderDto>(Ct))!.Total);
    }

    [Fact]
    public async Task Double_checkout_creates_one_order()
    {
        var m = await ArrangeAsync();
        var cart = await AddAsync(m.Customer, m.A1.Id, 2);
        var body = AsSeen(cart, m.Address.Id);

        // Two requests at the same moment (a double click): the cart lock lets one through.
        var responses = await Task.WhenAll(
            m.Customer.PostAsJsonAsync("/api/v1/orders/checkout", body, Ct),
            m.Customer.PostAsJsonAsync("/api/v1/orders/checkout", body, Ct));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);   // "Your cart is empty."

        var orders = (await m.Customer.GetFromJsonAsync<PagedResponse<OrderListItemDto>>("/api/v1/orders", Ct))!;
        Assert.Equal(1, orders.TotalCount);
        Assert.Equal(2, (await GetOfferAsync(m.SellerA, m.A1.Id)).ReservedStock);
    }

    [Fact]
    public async Task Cart_rules_and_roles()
    {
        var m = await ArrangeAsync();

        // Business rule 10: 1 to 10 per line, never above available stock.
        await AssertValidationErrorAsync(await m.Customer.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest { OfferId = m.A2.Id, Quantity = 11 }, Ct), "quantity");
        Assert.Equal(HttpStatusCode.Conflict,
            (await m.Customer.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest { OfferId = m.A1.Id, Quantity = 6 }, Ct)).StatusCode);
        await AddAsync(m.Customer, m.A2.Id, 9);
        await AssertValidationErrorAsync(await m.Customer.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest { OfferId = m.A2.Id, Quantity = 2 }, Ct), "quantity");
        await AssertValidationErrorAsync(await m.Customer.PostAsJsonAsync("/api/v1/cart/items", new AddCartItemRequest { OfferId = 999_999, Quantity = 1 }, Ct), "offerId");

        // Another customer's line is 404; sellers and admins have no cart; anonymous is 401.
        var cart = (await m.Customer.GetFromJsonAsync<CartDto>("/api/v1/cart", Ct))!;
        var line = cart.Sellers.Single().Items.Single();
        var other = factory.CreateClient(await factory.RegisterCustomerAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/cart/items/{line.Id}", new UpdateCartItemRequest { Quantity = 1 }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/cart/items/{line.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await m.SellerA.GetAsync("/api/v1/cart", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await m.Admin.PostAsJsonAsync("/api/v1/orders/checkout", AsSeen(cart, 1), Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/cart", Ct)).StatusCode);

        // A line whose offer leaves the catalog stays in the cart, flagged; checkout is blocked.
        Assert.Equal(HttpStatusCode.NoContent, (await m.SellerA.DeleteAsync($"/api/v1/seller/offers/{m.A2.Id}", Ct)).StatusCode);
        cart = (await m.Customer.GetFromJsonAsync<CartDto>("/api/v1/cart", Ct))!;
        Assert.Equal(CartIssues.Unavailable, cart.Sellers.Single().Items.Single().Issue);
        Assert.False(cart.CanCheckout);

        var checkout = await m.Customer.PostAsJsonAsync("/api/v1/orders/checkout", AsSeen(cart, m.Address.Id), Ct);
        Assert.Equal(HttpStatusCode.Conflict, checkout.StatusCode);

        // Someone else's address cannot be used; remove and clear work.
        var otherAddress = await TestCatalog.PostAsync<AddressDto>(other, "/api/v1/account/addresses",
            new CreateAddressRequest { City = "Irbid", Street = "Other Street 2", Phone = "+962790000002", IsDefault = true });
        await AddAsync(m.Customer, m.B1.Id, 1);
        cart = (await m.Customer.GetFromJsonAsync<CartDto>("/api/v1/cart", Ct))!;
        await AssertValidationErrorAsync(await m.Customer.PostAsJsonAsync("/api/v1/orders/checkout", AsSeen(cart, otherAddress.Id), Ct), "addressId");

        Assert.Equal(HttpStatusCode.NoContent, (await m.Customer.DeleteAsync($"/api/v1/cart/items/{line.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await m.Customer.DeleteAsync("/api/v1/cart", Ct)).StatusCode);
        Assert.Equal(0, (await m.Customer.GetFromJsonAsync<CartSummaryDto>("/api/v1/cart/summary", Ct))!.ItemCount);
    }

    [Fact]
    public async Task Order_keeps_the_shipping_address_snapshot_after_the_address_changes()
    {
        var m = await ArrangeAsync();
        var cart = await AddAsync(m.Customer, m.A1.Id, 1);
        var order = await TestCatalog.PostAsync<OrderDto>(m.Customer, "/api/v1/orders/checkout", AsSeen(cart, m.Address.Id));
        var me = (await m.Customer.GetFromJsonAsync<UserDto>("/api/v1/auth/me", Ct))!;

        // The customer edits the address, then deletes it.
        Assert.Equal(HttpStatusCode.OK, (await m.Customer.PutAsJsonAsync($"/api/v1/account/addresses/{m.Address.Id}", new UpdateAddressRequest
        {
            City = "Aqaba",
            Street = "Changed Street 9",
            Details = "Changed",
            Phone = "+962799999999"
        }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await m.Customer.DeleteAsync($"/api/v1/account/addresses/{m.Address.Id}", Ct)).StatusCode);

        // Customer and seller still see the address exactly as it was at checkout.
        var expected = new ShippingAddressDto(m.Address.Id, me.FullName, "Amman", "Test Street 1", null, "+962790000001");
        var customerView = (await m.Customer.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{order.Id}", Ct))!;
        Assert.Equal(expected, customerView.ShippingAddress);

        var sellerView = (await m.SellerA.GetFromJsonAsync<SellerOrderDetailsDto>(
            $"/api/v1/seller/orders/{Assert.Single(order.SellerOrders).Id}", Ct))!;
        Assert.Equal(expected, sellerView.ShippingAddress);
    }

    [Fact]
    public async Task Suspended_seller_cannot_sell_but_still_finishes_accepted_orders()
    {
        var m = await ArrangeAsync();
        var cart = await AddAsync(m.Customer, m.A1.Id, 1);
        var order = await TestCatalog.PostAsync<OrderDto>(m.Customer, "/api/v1/orders/checkout", AsSeen(cart, m.Address.Id));
        var sub = Assert.Single(order.SellerOrders);

        var suspend = await m.Admin.PostAsJsonAsync($"/api/v1/admin/sellers/{sub.SellerId}/suspend",
            new SellerStatusReasonRequest { Reason = "Late shipments" }, Ct);
        Assert.Equal(HttpStatusCode.OK, suspend.StatusCode);

        // The access token is still valid; the policy reads the status from the database.
        Assert.Equal(HttpStatusCode.Forbidden, (await m.SellerA.GetAsync("/api/v1/seller/offers", Ct)).StatusCode);

        // Existing sub-orders stay reachable and can move forward to delivery.
        Assert.Equal(HttpStatusCode.OK, (await m.SellerA.GetAsync($"/api/v1/seller/orders/{sub.Id}", Ct)).StatusCode);
        await MoveAsync(m.SellerA, sub.Id, "Confirmed");
        await MoveAsync(m.SellerA, sub.Id, "Shipped");
        var delivered = await MoveAsync(m.SellerA, sub.Id, "Delivered");
        Assert.Equal("Delivered", delivered.Status);
    }

    [Fact]
    public async Task Unconfirmed_sub_orders_are_cancelled_after_the_expiry_and_release_stock()
    {
        var m = await ArrangeAsync();

        // One order with two parts: seller A's part stays Pending, seller B confirms theirs.
        await AddAsync(m.Customer, m.A1.Id, 2);
        var cart = await AddAsync(m.Customer, m.B1.Id, 1);
        var order = await TestCatalog.PostAsync<OrderDto>(m.Customer, "/api/v1/orders/checkout", AsSeen(cart, m.Address.Id));
        var partA = order.SellerOrders.Single(s => s.Items.Any(i => i.OfferId == m.A1.Id));
        var partB = order.SellerOrders.Single(s => s.Items.Any(i => i.OfferId == m.B1.Id));
        await MoveAsync(m.SellerB, partB.Id, "Confirmed");

        // A second, fresh order of seller A stays untouched (younger than the expiry).
        var fresh = await TestCatalog.PostAsync<OrderDto>(m.Customer, "/api/v1/orders/checkout",
            AsSeen(await AddAsync(m.Customer, m.A2.Id, 1), m.Address.Id));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PcPartsContext>();
        var oldDate = DateTime.UtcNow.AddDays(-4);
        await db.SellerOrders.Where(s => s.OrderId == order.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.CreatedAt, oldDate), Ct);

        var cancelled = await scope.ServiceProvider.GetRequiredService<PendingOrderExpiry>().CancelExpiredAsync(3, 1000, Ct);
        Assert.Equal(1, cancelled);

        var after = (await m.Customer.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{order.Id}", Ct))!;
        var cancelledPart = after.SellerOrders.Single(s => s.Id == partA.Id);
        Assert.Equal(("Cancelled", "System: Not confirmed within 3 days"), (cancelledPart.Status, cancelledPart.CancelReason));
        Assert.Equal("Confirmed", after.SellerOrders.Single(s => s.Id == partB.Id).Status);
        Assert.Equal("Pending", (await m.Customer.GetFromJsonAsync<OrderDto>($"/api/v1/orders/{fresh.Id}", Ct))!.SellerOrders.Single().Status);

        // Seller A's 2 units are available again; the fresh order still holds its unit of A2.
        var a1 = await GetOfferAsync(m.SellerA, m.A1.Id);
        Assert.Equal((5, 0), (a1.Stock, a1.ReservedStock));
        Assert.Equal(1, (await GetOfferAsync(m.SellerA, m.A2.Id)).ReservedStock);

        // Running again finds nothing new.
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PendingOrderExpiry>().CancelExpiredAsync(3, 1000, Ct));
    }

    [Fact]
    public async Task Status_rules_ownership_and_admin_cancel()
    {
        var m = await ArrangeAsync();
        var cart = await AddAsync(m.Customer, m.A1.Id, 1);
        var order = await TestCatalog.PostAsync<OrderDto>(m.Customer, "/api/v1/orders/checkout", AsSeen(cart, m.Address.Id));
        var sub = Assert.Single(order.SellerOrders);
        var path = $"/api/v1/seller/orders/{sub.Id}/status";

        // No skipping steps; a seller cancelling must give a reason; unknown status is 400.
        Assert.Equal(HttpStatusCode.Conflict,
            (await m.SellerA.PatchAsJsonAsync(path, new ChangeSellerOrderStatusRequest { Status = "Shipped" }, Ct)).StatusCode);
        await AssertValidationErrorAsync(await m.SellerA.PatchAsJsonAsync(path, new ChangeSellerOrderStatusRequest { Status = "Cancelled" }, Ct), "reason");
        await AssertValidationErrorAsync(await m.SellerA.PatchAsJsonAsync(path, new ChangeSellerOrderStatusRequest { Status = "Lost" }, Ct), "status");

        // Another seller and another customer cannot see or touch it (404).
        Assert.Equal(HttpStatusCode.NotFound,
            (await m.SellerB.PatchAsJsonAsync(path, new ChangeSellerOrderStatusRequest { Status = "Confirmed" }, Ct)).StatusCode);
        var other = factory.CreateClient(await factory.RegisterCustomerAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/orders/{order.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/v1/orders/{order.Id}/cancel", new CancelOrderRequest(), Ct)).StatusCode);

        // Once shipped, the customer can no longer cancel.
        await MoveAsync(m.SellerA, sub.Id, "Confirmed");
        await MoveAsync(m.SellerA, sub.Id, "Shipped");
        Assert.Equal(HttpStatusCode.Conflict, (await m.Customer.PostAsJsonAsync($"/api/v1/orders/{order.Id}/cancel", new CancelOrderRequest(), Ct)).StatusCode);

        // Admin: finds the order by number, sees the customer, and can finish or cancel any open part.
        var number = order.OrderNumber["PCM-".Length..];
        var found = (await m.Admin.GetFromJsonAsync<PagedResponse<OrderListItemDto>>($"/api/v1/admin/orders?q={number}&status=Shipped", Ct))!;
        Assert.Equal(order.Id, Assert.Single(found.Items).Id);
        Assert.Equal("Test Customer", found.Items[0].Customer!.FullName);

        var adminView = (await m.Admin.GetFromJsonAsync<OrderDto>($"/api/v1/admin/orders/{order.Id}", Ct))!;
        Assert.Equal(new[] { "Delivered" }, adminView.SellerOrders.Single().NextStatuses);

        var second = await TestCatalog.PostAsync<OrderDto>(m.Customer, "/api/v1/orders/checkout",
            AsSeen(await AddAsync(m.Customer, m.B1.Id, 2), m.Address.Id));
        var secondSub = Assert.Single(second.SellerOrders);
        var adminPath = $"/api/v1/admin/orders/{second.Id}/seller-orders/{secondSub.Id}/status";
        await AssertValidationErrorAsync(await m.Admin.PatchAsJsonAsync(adminPath, new ChangeSellerOrderStatusRequest { Status = "Cancelled" }, Ct), "reason");

        var cancelled = await PatchAsync<OrderDto>(m.Admin, adminPath, new ChangeSellerOrderStatusRequest { Status = "Cancelled", Reason = "Seller suspended" });
        Assert.Equal("Admin: Seller suspended", cancelled.SellerOrders.Single().CancelReason);
        Assert.Equal(0, (await GetOfferAsync(m.SellerB, m.B1.Id)).ReservedStock);

        // Customers and sellers cannot use the admin endpoints.
        Assert.Equal(HttpStatusCode.Forbidden, (await m.Customer.GetAsync("/api/v1/admin/orders", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await m.SellerA.GetAsync("/api/v1/admin/orders", Ct)).StatusCode);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Expected 400 for '{field}' but got {(int)response.StatusCode}: {body}");

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Ct);
        Assert.True(problem!.Errors.ContainsKey(field), $"Expected an error for '{field}': {body}");
    }
}
