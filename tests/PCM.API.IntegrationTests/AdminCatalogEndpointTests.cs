using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;

namespace PCM.API.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class AdminCatalogEndpointTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Admin_builds_a_category_and_its_product_appears_in_the_catalog()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        Assert.Equal(["Alpha", "Beta"], test.Kind.Options.Select(o => o.Value));
        Assert.Equal("kind", test.Kind.Slug);

        var created = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Beta, 32, name: "Gizmo Pro 32"));

        Assert.Equal("Approved", created.Status);
        Assert.True(created.IsActive);
        Assert.StartsWith("gizmo-pro-32", created.Slug);
        Assert.Equal("Beta", created.Specs.Single(s => s.AttributeId == test.Kind.Id).DisplayValue);
        Assert.Equal("32 GB", created.Specs.Single(s => s.AttributeId == test.Size.Id).DisplayValue);

        // Public search: category + enum + number filters.
        var anonymous = factory.CreateClient();
        var slug = test.Category.Slug;
        Assert.Equal(1, (await SearchAsync(anonymous, $"category={slug}&kind=beta&size=16-64")).TotalCount);
        Assert.Equal(0, (await SearchAsync(anonymous, $"category={slug}&kind=alpha")).TotalCount);
        Assert.Equal(created.Slug, (await anonymous.GetFromJsonAsync<ProductDetailsDto>($"/api/v1/products/by-slug/{created.Slug}", Ct))!.Slug);

        // Deactivate hides it; activate shows it again.
        await TestCatalog.PostAsync<ProductManageDto>(admin, $"/api/v1/admin/products/{created.Id}/deactivate", body: null);
        Assert.Equal(0, (await SearchAsync(anonymous, $"category={slug}")).TotalCount);
        await TestCatalog.PostAsync<ProductManageDto>(admin, $"/api/v1/admin/products/{created.Id}/activate", body: null);
        Assert.Equal(1, (await SearchAsync(anonymous, $"category={slug}")).TotalCount);
    }

    [Fact]
    public async Task Update_replaces_specs_and_images_and_renames_the_slug()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var created = await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Alpha, 8));

        var request = test.NewProduct(test.Beta, 16, name: $"Renamed Part {Guid.NewGuid():N}"[..22]) with
        {
            ModelNumber = $"RN-{Guid.NewGuid():N}"[..20],
            Specs =
            [
                new SpecValueRequest { AttributeId = test.Kind.Id, OptionId = test.Beta },
                new SpecValueRequest { AttributeId = test.Size.Id, NumberValue = 16 },
                new SpecValueRequest { AttributeId = test.Notes.Id, TextValue = "Now with notes" }
            ],
            Images = [new ProductImageRequest { Url = "https://cdn.example.com/a.png" }, new ProductImageRequest { Url = "https://cdn.example.com/b.png" }]
        };

        var updated = await TestCatalog.PutAsync<ProductManageDto>(admin, $"/api/v1/admin/products/{created.Id}", request);

        Assert.NotEqual(created.Slug, updated.Slug);
        Assert.StartsWith("renamed-part", updated.Slug);
        Assert.Equal(3, updated.Specs.Count);
        Assert.Equal("Now with notes", updated.Specs.Single(s => s.AttributeId == test.Notes.Id).DisplayValue);
        Assert.Equal(["https://cdn.example.com/a.png", "https://cdn.example.com/b.png"], updated.Images.Select(i => i.Url));
    }

    [Fact]
    public async Task Spec_rules_are_checked_against_the_category()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var other = await TestCatalog.CreateCategoryAsync(admin);

        // Option of another attribute (the other category's "Kind").
        var wrongOption = test.NewProduct(test.Alpha, 8) with
        {
            Specs = [new SpecValueRequest { AttributeId = test.Kind.Id, OptionId = other.Alpha }, new SpecValueRequest { AttributeId = test.Size.Id, NumberValue = 8 }]
        };
        await AssertBadRequestAsync(admin, wrongOption, "specs[0].optionId");

        // Missing required attribute (Size).
        var missing = test.NewProduct(test.Alpha, 8) with { Specs = [new SpecValueRequest { AttributeId = test.Kind.Id, OptionId = test.Alpha }] };
        await AssertBadRequestAsync(admin, missing, "specs");

        // Text value for a number attribute.
        var wrongType = test.NewProduct(test.Alpha, 8) with
        {
            Specs = [new SpecValueRequest { AttributeId = test.Kind.Id, OptionId = test.Alpha }, new SpecValueRequest { AttributeId = test.Size.Id, TextValue = "eight" }]
        };
        await AssertBadRequestAsync(admin, wrongType, "specs[1].numberValue");

        // Products belong to leaf categories only: "components" has children.
        var components = (await admin.GetFromJsonAsync<List<CategoryTreeDto>>("/api/v1/categories", Ct))!.Single(c => c.Slug == "components");
        await AssertBadRequestAsync(admin, test.NewProduct(test.Alpha, 8) with { CategoryId = components.Id }, "categoryId");
    }

    [Fact]
    public async Task Same_model_number_for_the_same_brand_is_a_conflict()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        var first = test.NewProduct(test.Alpha, 8);
        await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", first);

        var duplicate = test.NewProduct(test.Beta, 16) with { ModelNumber = first.ModelNumber };
        var response = await admin.PostAsJsonAsync("/api/v1/admin/products", duplicate, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Category_rules_return_409()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);
        await TestCatalog.PostAsync<ProductManageDto>(admin, "/api/v1/admin/products", test.NewProduct(test.Alpha, 8));

        // A category with products (and attributes) cannot get children or be deleted.
        var child = await admin.PostAsJsonAsync("/api/v1/admin/categories", new CreateCategoryRequest { ParentId = test.Category.Id, Name = "Child" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, child.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/categories/{test.Category.Id}", Ct)).StatusCode);

        // Used attributes and options cannot be deleted, and a used attribute cannot change type.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/categories/{test.Category.Id}/attributes/{test.Kind.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/attributes/{test.Kind.Id}/options/{test.Alpha}", Ct)).StatusCode);
        var retype = await admin.PutAsJsonAsync($"/api/v1/admin/categories/{test.Category.Id}/attributes/{test.Size.Id}",
            new SpecAttributeRequest { Name = "Size", DataType = "Text" }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, retype.StatusCode);

        // An unused option and an unused attribute can go.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/attributes/{test.Kind.Id}/options/{test.Beta}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/categories/{test.Category.Id}/attributes/{test.Notes.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Attribute_names_that_clash_with_search_keys_are_rejected()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var test = await TestCatalog.CreateCategoryAsync(admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/admin/categories/{test.Category.Id}/attributes",
            new SpecAttributeRequest { Name = "Brand", DataType = "Enum" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Empty_category_can_be_renamed_and_deleted()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var created = await TestCatalog.PostAsync<CategoryDto>(admin, "/api/v1/admin/categories",
            new CreateCategoryRequest { Name = $"Temp {Guid.NewGuid():N}"[..13] });

        var renamed = await TestCatalog.PutAsync<CategoryDto>(admin, $"/api/v1/admin/categories/{created.Id}",
            new UpdateCategoryRequest { Name = $"Renamed {Guid.NewGuid():N}"[..16], SortOrder = 5 });
        Assert.StartsWith("renamed-", renamed.Slug);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/categories/{created.Id}", Ct)).StatusCode);
        var tree = await admin.GetFromJsonAsync<List<CategoryTreeDto>>("/api/v1/categories", Ct);
        Assert.DoesNotContain(tree!, c => c.Id == created.Id);
    }

    [Fact]
    public async Task Brand_crud_with_slug_and_in_use_conflict()
    {
        var admin = factory.CreateClient(await factory.CreateAdminAsync());
        var name = $"Acme {Guid.NewGuid():N}"[..13];

        var brand = await TestCatalog.PostAsync<BrandDto>(admin, "/api/v1/admin/brands", new BrandRequest { Name = name });
        Assert.Equal(name.ToLowerInvariant().Replace(' ', '-'), brand.Slug);

        var duplicate = await admin.PostAsJsonAsync("/api/v1/admin/brands", new BrandRequest { Name = name.ToUpperInvariant() }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var updated = await TestCatalog.PutAsync<BrandDto>(admin, $"/api/v1/admin/brands/{brand.Id}",
            new BrandRequest { Name = name, LogoUrl = "https://cdn.example.com/acme.png" });
        Assert.Equal("https://cdn.example.com/acme.png", updated.LogoUrl);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/brands/{brand.Id}", Ct)).StatusCode);

        // Seeded brand with products.
        var amd = (await admin.GetFromJsonAsync<PagedResponse<BrandDto>>("/api/v1/brands?pageSize=100", Ct))!.Items.Single(b => b.Slug == "amd");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/v1/admin/brands/{amd.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Catalog_admin_endpoints_need_the_admin_role()
    {
        var customer = factory.CreateClient(await factory.RegisterCustomerAsync());
        var anonymous = factory.CreateClient();
        var body = new CreateCategoryRequest { Name = "Nope" };

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync("/api/v1/admin/categories", body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/admin/categories", body, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/products", Ct)).StatusCode);
    }

    // ---------------------------------------------------------------------

    private static async Task<PagedResponse<ProductListItemDto>> SearchAsync(HttpClient client, string queryString) =>
        (await client.GetFromJsonAsync<PagedResponse<ProductListItemDto>>($"/api/v1/products?{queryString}", Ct))!;

    private static async Task AssertBadRequestAsync(HttpClient admin, ProductUpsertRequest request, string field)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/products", request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"Expected 400 for {field} but got {(int)response.StatusCode}: {body}");
        using var problem = JsonDocument.Parse(body);
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"No error for '{field}': {body}");
    }
}
