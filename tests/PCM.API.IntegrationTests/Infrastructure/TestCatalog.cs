using System.Net.Http.Json;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Sellers;

namespace PCM.API.IntegrationTests.Infrastructure;

/// <summary>
/// A throwaway top-level category with its own attributes and brand, created through the admin API.
/// Tests that write products use one of these, so the seeded catalog counts stay exact.
/// </summary>
public sealed record TestCategory(
    CategoryDto Category,
    SpecAttributeDto Kind,       // Enum, filterable, required: options alpha, beta
    SpecAttributeDto Size,       // Number (GB), filterable, required
    SpecAttributeDto Notes,      // Text, optional
    BrandDto Brand)
{
    public int Alpha => Kind.Options.Single(o => o.Slug == "alpha").Id;

    public int Beta => Kind.Options.Single(o => o.Slug == "beta").Id;

    /// <summary>A valid product request; every call gets a unique name and model number.</summary>
    public ProductUpsertRequest NewProduct(int optionId, decimal size, string? name = null) => new()
    {
        CategoryId = Category.Id,
        BrandId = Brand.Id,
        Name = name ?? $"Test Part {Guid.NewGuid():N}"[..24],
        ModelNumber = $"TP-{Guid.NewGuid():N}"[..20],
        Description = "Created by an integration test.",
        Specs =
        [
            new SpecValueRequest { AttributeId = Kind.Id, OptionId = optionId },
            new SpecValueRequest { AttributeId = Size.Id, NumberValue = size }
        ],
        Images = [new ProductImageRequest { Url = "/images/products/placeholder.svg", AltText = "Test part" }]
    };
}

public static class TestCatalog
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<TestCategory> CreateCategoryAsync(HttpClient admin)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];

        var category = await PostAsync<CategoryDto>(admin, "/api/v1/admin/categories",
            new CreateCategoryRequest { Name = $"Test Category {suffix}", SortOrder = 99 });

        var attributesPath = $"/api/v1/admin/categories/{category.Id}/attributes";
        var kind = await PostAsync<SpecAttributeDto>(admin, attributesPath,
            new SpecAttributeRequest { Name = "Kind", DataType = "Enum", IsFilterable = true, IsRequired = true, SortOrder = 1 });
        var size = await PostAsync<SpecAttributeDto>(admin, attributesPath,
            new SpecAttributeRequest { Name = "Size", DataType = "Number", Unit = "GB", IsFilterable = true, IsRequired = true, SortOrder = 2 });
        var notes = await PostAsync<SpecAttributeDto>(admin, attributesPath,
            new SpecAttributeRequest { Name = "Notes", DataType = "Text", SortOrder = 3 });

        await PostAsync<SpecOptionDto>(admin, $"/api/v1/admin/attributes/{kind.Id}/options", new SpecOptionRequest { Value = "Alpha", SortOrder = 1 });
        await PostAsync<SpecOptionDto>(admin, $"/api/v1/admin/attributes/{kind.Id}/options", new SpecOptionRequest { Value = "Beta", SortOrder = 2 });

        var attributes = await admin.GetFromJsonAsync<List<SpecAttributeDto>>($"/api/v1/categories/{category.Id}/attributes", Ct);
        var brand = await PostAsync<BrandDto>(admin, "/api/v1/admin/brands", new BrandRequest { Name = $"Test Brand {suffix}" });

        return new TestCategory(
            category,
            attributes!.Single(a => a.Id == kind.Id),
            attributes!.Single(a => a.Id == size.Id),
            attributes!.Single(a => a.Id == notes.Id),
            brand);
    }

    /// <summary>Registers a seller and approves them through the admin API.</summary>
    public static async Task<AuthResponse> CreateApprovedSellerAsync(this ApiFactory factory, HttpClient admin)
    {
        var seller = await factory.RegisterSellerAsync();
        var pending = await admin.GetFromJsonAsync<PagedResponse<SellerAdminDto>>("/api/v1/admin/sellers?status=Pending&pageSize=100&sort=newest", Ct);
        var sellerId = pending!.Items.Single(s => s.UserId == seller.User.Id).Id;

        await PostAsync<SellerAdminDto>(admin, $"/api/v1/admin/sellers/{sellerId}/approve", body: null);
        return seller;
    }

    public static async Task<T> PostAsync<T>(HttpClient client, string url, object? body)
    {
        var response = body is null
            ? await client.PostAsync(url, null, Ct)
            : await client.PostAsJsonAsync(url, body, body.GetType(), Ct);

        Assert.True(response.IsSuccessStatusCode, $"POST {url} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }

    public static async Task<T> PutAsync<T>(HttpClient client, string url, object body)
    {
        var response = await client.PutAsJsonAsync(url, body, body.GetType(), Ct);

        Assert.True(response.IsSuccessStatusCode, $"PUT {url} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(Ct)}");
        return (await response.Content.ReadFromJsonAsync<T>(Ct))!;
    }
}
