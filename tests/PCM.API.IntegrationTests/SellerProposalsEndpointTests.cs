using System.Net;
using System.Net.Http.Json;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;

namespace PCM.API.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class SellerProposalsEndpointTests(ApiFactory factory)
{
    private const string BasePath = "/api/v1/seller/products/proposals";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Proposal_lifecycle_pending_rejected_resubmitted_approved()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var seller = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));
        var anonymous = factory.CreateClient();

        // 1. Seller proposes: Pending, not in the public catalog.
        var proposal = await TestCatalog.PostAsync<ProductManageDto>(seller, BasePath, test.NewProduct(test.Alpha, 8));
        Assert.Equal("Pending", proposal.Status);
        Assert.NotNull(proposal.ProposedByStoreName);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/v1/products/by-slug/{proposal.Slug}", Ct)).StatusCode);

        // 2. It shows in the admin review queue.
        var queue = await admin.GetFromJsonAsync<PagedResponse<ProductManageListItemDto>>("/api/v1/admin/products?status=pending&pageSize=100", Ct);
        Assert.Contains(queue!.Items, p => p.Id == proposal.Id);

        // 3. Admin rejects with a note; the seller sees it.
        await TestCatalog.PostAsync<ProductManageDto>(admin, $"/api/v1/admin/products/{proposal.Id}/reject", new ProductReviewRequest { Note = "Model number is wrong" });
        var rejected = await seller.GetFromJsonAsync<ProductManageDto>($"{BasePath}/{proposal.Id}", Ct);
        Assert.Equal("Rejected", rejected!.Status);
        Assert.Equal("Model number is wrong", rejected.ReviewNote);

        // Reviewing twice is not allowed.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/admin/products/{proposal.Id}/approve", null, Ct)).StatusCode);

        // 4. Seller fixes it: back to Pending, note cleared.
        var fixedRequest = test.NewProduct(test.Alpha, 8);
        var resubmitted = await TestCatalog.PutAsync<ProductManageDto>(seller, $"{BasePath}/{proposal.Id}", fixedRequest);
        Assert.Equal("Pending", resubmitted.Status);
        Assert.Null(resubmitted.ReviewNote);

        // 5. Admin approves: public, and the seller can no longer edit it.
        await TestCatalog.PostAsync<ProductManageDto>(admin, $"/api/v1/admin/products/{proposal.Id}/approve", body: null);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/v1/products/by-slug/{resubmitted.Slug}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await seller.PutAsJsonAsync($"{BasePath}/{proposal.Id}", fixedRequest, Ct)).StatusCode);

        var mine = await seller.GetFromJsonAsync<PagedResponse<ProductManageListItemDto>>($"{BasePath}?status=Approved", Ct);
        Assert.Contains(mine!.Items, p => p.Id == proposal.Id);
    }

    [Fact]
    public async Task Other_sellers_proposals_return_404()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var owner = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));
        var other = factory.CreateClient(await factory.CreateApprovedSellerAsync(admin));

        var proposal = await TestCatalog.PostAsync<ProductManageDto>(owner, BasePath, test.NewProduct(test.Beta, 4));

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{BasePath}/{proposal.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"{BasePath}/{proposal.Id}", test.NewProduct(test.Beta, 4), Ct)).StatusCode);

        var othersList = await other.GetFromJsonAsync<PagedResponse<ProductManageListItemDto>>(BasePath, Ct);
        Assert.DoesNotContain(othersList!.Items, p => p.Id == proposal.Id);
    }

    [Fact]
    public async Task Only_approved_sellers_can_propose()
    {
        var pendingSeller = factory.CreateClient(await factory.RegisterSellerAsync());
        var customer = factory.CreateClient(await factory.RegisterCustomerAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await pendingSeller.GetAsync(BasePath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(BasePath, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(BasePath, Ct)).StatusCode);
    }
}
