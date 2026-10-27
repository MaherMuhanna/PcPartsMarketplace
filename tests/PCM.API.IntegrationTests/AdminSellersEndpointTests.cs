using System.Net;
using System.Net.Http.Json;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Sellers;

namespace PCM.API.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class AdminSellersEndpointTests(ApiFactory factory)
{
    private const string BasePath = "/api/v1/admin/sellers";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_sees_pending_sellers_with_owner_details()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var seller = await factory.RegisterSellerAsync();

        var page = await admin.GetFromJsonAsync<PagedResponse<SellerAdminDto>>($"{BasePath}?status=Pending&pageSize=100", Ct);

        var row = Assert.Single(page!.Items, s => s.UserId == seller.User.Id);
        Assert.Equal("Pending", row.Status);
        Assert.Equal(seller.User.Email, row.OwnerEmail);
        Assert.Equal("Test Seller", row.OwnerFullName);
    }

    [Fact]
    public async Task Full_lifecycle_approve_suspend_reinstate()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var sellerAuth = await factory.RegisterSellerAsync();
        var sellerId = await GetSellerIdAsync(admin, sellerAuth);

        var approved = await PostAsync<SellerAdminDto>(admin, $"{BasePath}/{sellerId}/approve", body: null);
        Assert.Equal("Approved", approved.Status);
        Assert.NotNull(approved.ApprovedAt);

        // Approving twice is not a valid transition.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"{BasePath}/{sellerId}/approve", null, Ct)).StatusCode);

        var suspended = await PostAsync<SellerAdminDto>(admin, $"{BasePath}/{sellerId}/suspend", new SellerStatusReasonRequest { Reason = "Customer complaints" });
        Assert.Equal("Suspended", suspended.Status);
        Assert.Equal("Customer complaints", suspended.StatusReason);

        var reinstated = await PostAsync<SellerAdminDto>(admin, $"{BasePath}/{sellerId}/reinstate", body: null);
        Assert.Equal("Approved", reinstated.Status);
        Assert.Null(reinstated.StatusReason);
        // The first approval time is kept (the database stores milliseconds).
        Assert.Equal(approved.ApprovedAt!.Value, reinstated.ApprovedAt!.Value, TimeSpan.FromMilliseconds(1));

        // Times loaded from the database are returned as UTC ("...Z"), like freshly set ones.
        Assert.Equal(DateTimeKind.Utc, reinstated.ApprovedAt.Value.Kind);
        Assert.Equal(DateTimeKind.Utc, reinstated.CreatedAt.Kind);
    }

    [Fact]
    public async Task Suspending_signs_the_seller_out()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var sellerAuth = await factory.RegisterSellerAsync();
        var sellerId = await GetSellerIdAsync(admin, sellerAuth);
        await PostAsync<SellerAdminDto>(admin, $"{BasePath}/{sellerId}/approve", body: null);

        await PostAsync<SellerAdminDto>(admin, $"{BasePath}/{sellerId}/suspend", new SellerStatusReasonRequest { Reason = "Policy violation" });

        var refresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest { RefreshToken = sellerAuth.RefreshToken }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Reject_requires_a_reason_and_is_final()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var sellerAuth = await factory.RegisterSellerAsync();
        var sellerId = await GetSellerIdAsync(admin, sellerAuth);

        var noReason = await admin.PostAsJsonAsync($"{BasePath}/{sellerId}/reject", new SellerStatusReasonRequest { Reason = "" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var rejected = await PostAsync<SellerAdminDto>(admin, $"{BasePath}/{sellerId}/reject", new SellerStatusReasonRequest { Reason = "Missing store details" });
        Assert.Equal("Rejected", rejected.Status);

        // No transition leaves Rejected in V1.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"{BasePath}/{sellerId}/approve", null, Ct)).StatusCode);

        // The seller can still sign in and read the reason.
        var profile = await factory.CreateClient(sellerAuth).GetFromJsonAsync<SellerProfileDto>("/api/v1/seller/profile", Ct);
        Assert.Equal("Missing store details", profile!.StatusReason);
    }

    [Fact]
    public async Task Unknown_seller_returns_404()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"{BasePath}/999999/approve", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Non_admins_get_403()
    {
        var customer = factory.CreateClient(await factory.RegisterCustomerAsync());
        var seller = factory.CreateClient(await factory.RegisterSellerAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(BasePath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync(BasePath, Ct)).StatusCode);
    }

    [Fact]
    public async Task Invalid_status_filter_returns_400()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"{BasePath}?status=Banned", Ct)).StatusCode);
    }

    private static async Task<int> GetSellerIdAsync(HttpClient admin, AuthResponse seller)
    {
        var page = await admin.GetFromJsonAsync<PagedResponse<SellerAdminDto>>($"{BasePath}?status=Pending&pageSize=100", Ct);
        return page!.Items.Single(s => s.UserId == seller.User.Id).Id;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string url, object? body)
    {
        var response = body is null
            ? await client.PostAsync(url, null, Ct)
            : await client.PostAsJsonAsync(url, body, Ct);

        Assert.True(response.IsSuccessStatusCode, $"{url} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
