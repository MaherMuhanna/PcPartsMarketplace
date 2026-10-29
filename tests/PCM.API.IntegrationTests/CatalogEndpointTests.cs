using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;

namespace PCM.API.IntegrationTests;

/// <summary>
/// Public catalog against the data in Database/seed.sql. Tests that write products use their own
/// category (see TestCatalog), so the seeded counts below stay exact.
/// </summary>
[Collection(nameof(ApiCollection))]
public sealed class CatalogEndpointTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Category_tree_has_the_seeded_leaves_under_components()
    {
        var tree = await _client.GetFromJsonAsync<List<CategoryTreeDto>>("/api/v1/categories", Ct);

        var components = Assert.Single(tree!, c => c.Slug == "components");
        Assert.False(components.IsLeaf);
        Assert.Equal(["cpu", "gpu", "ram", "storage", "motherboard", "power-supply"], components.Children.Select(c => c.Slug));
        Assert.All(components.Children, c => Assert.True(c.IsLeaf));
    }

    [Theory]
    [InlineData("category=components", 36)]          // parent category includes every leaf below it
    [InlineData("category=cpu", 6)]                  // the inactive i9-13900K is hidden
    [InlineData("category=ram&memory-type=ddr4", 1)]
    [InlineData("category=ram&memory-type=ddr4,ddr5", 6)]
    [InlineData("category=ram&memory-type=ddr4&memory-type=ddr5", 6)]
    [InlineData("category=ram&capacity=32,64", 4)]           // picked values (OR)
    [InlineData("category=ram&capacity=16&speed=5200", 1)]
    [InlineData("category=ram&capacity=32-64", 4)]           // ranges still work for hand-written URLs
    [InlineData("category=ram&capacity=32-", 4)]
    [InlineData("category=ram&capacity=-16", 2)]
    [InlineData("category=ram&memory-type=ddr5&capacity=16", 1)]  // AND between attributes
    [InlineData("category=ram&brand=kingston", 2)]
    [InlineData("category=ram&brand=kingston,corsair", 4)]
    [InlineData("category=ram&brand=no-such-brand", 0)]
    [InlineData("category=ram&memory-type=ddr6", 0)]  // unknown option matches nothing
    [InlineData("category=ram&not-an-attribute=x", 6)] // unknown keys are ignored
    [InlineData("category=cpu&socket=am5&cores=8-", 2)]
    [InlineData("q=ryzen", 3)]
    [InlineData("q=BX8071514700K", 1)]                // model number
    [InlineData("category=components&memory-type=ddr4", 36)] // spec filters need a leaf category
    public async Task Search_filters_by_category_brand_keyword_and_specs(string queryString, int expected)
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<ProductListItemDto>>($"/api/v1/products?{queryString}&pageSize=100", Ct);

        Assert.Equal(expected, page!.TotalCount);
    }

    [Fact]
    public async Task Search_sorts_by_name_and_pages()
    {
        var first = await _client.GetFromJsonAsync<PagedResponse<ProductListItemDto>>("/api/v1/products?category=cpu&sort=name&pageSize=4", Ct);
        var second = await _client.GetFromJsonAsync<PagedResponse<ProductListItemDto>>("/api/v1/products?category=cpu&sort=name&pageSize=4&page=2", Ct);

        Assert.Equal(6, first!.TotalCount);
        Assert.Equal(2, first.TotalPages);
        var names = first.Items.Concat(second!.Items).Select(p => p.Name).ToList();
        Assert.Equal(6, names.Distinct().Count());
        Assert.Equal(names.Order(StringComparer.OrdinalIgnoreCase), names);
    }

    [Fact]
    public async Task List_items_carry_brand_category_and_main_image()
    {
        var page = await _client.GetFromJsonAsync<PagedResponse<ProductListItemDto>>("/api/v1/products?q=7800X3D", Ct);

        var item = Assert.Single(page!.Items);
        Assert.Equal("amd-ryzen-7-7800x3d", item.Slug);
        Assert.Equal("amd", item.Brand.Slug);
        Assert.Equal("cpu", item.Category.Slug);
        Assert.Equal("/images/products/cpu.svg", item.MainImageUrl);
        Assert.Null(item.MinPrice);                  // offers arrive in Phase 4
        Assert.Equal(0, item.OfferCount);
    }

    [Theory]
    [InlineData("category=ram&capacity=abc", "capacity")]
    [InlineData("category=ram&capacity=64-16", "capacity")]
    [InlineData("category=ram&capacity=16,big", "capacity")]
    [InlineData("sort=rating", "sort")]
    [InlineData("minPrice=50&maxPrice=10", "maxPrice")]
    public async Task Bad_values_return_400_with_the_field_name(string queryString, string field)
    {
        var response = await _client.GetAsync($"/api/v1/products?{queryString}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(field, out _));
    }

    [Fact]
    public async Task Unknown_category_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/products?category=no-such-category", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/products/filters?category=no-such-category", Ct)).StatusCode);
    }

    [Fact]
    public async Task Filters_for_a_leaf_list_options_with_counts_and_number_ranges()
    {
        var filters = await _client.GetFromJsonAsync<CatalogFiltersDto>("/api/v1/products/filters?category=ram", Ct);

        Assert.True(filters!.IsLeaf);
        Assert.Equal(["components", "ram"], filters.Path.Select(c => c.Slug));

        var memoryType = Assert.Single(filters.Attributes, a => a.Slug == "memory-type");
        Assert.Equal(1, memoryType.Options.Single(o => o.Slug == "ddr4").Count);
        Assert.Equal(5, memoryType.Options.Single(o => o.Slug == "ddr5").Count);

        var capacity = Assert.Single(filters.Attributes, a => a.Slug == "capacity");
        Assert.Equal(16m, capacity.Min);
        Assert.Equal(64m, capacity.Max);
        Assert.Equal(["16", "32", "64"], capacity.Options.Select(o => o.Slug));
        Assert.Equal(["16 GB", "32 GB", "64 GB"], capacity.Options.Select(o => o.Value));
        Assert.Equal([2, 3, 1], capacity.Options.Select(o => o.Count));

        // CAS Latency stays in the specs but is not offered as a filter.
        Assert.DoesNotContain(filters.Attributes, a => a.Slug == "cas-latency");

        Assert.Equal(2, filters.Brands.Single(b => b.Slug == "corsair").Count);
        Assert.DoesNotContain(filters.Brands, b => b.Slug == "amd");
    }

    [Fact]
    public async Task Filters_for_a_parent_list_children_but_no_attributes()
    {
        var filters = await _client.GetFromJsonAsync<CatalogFiltersDto>("/api/v1/products/filters?category=components", Ct);

        Assert.False(filters!.IsLeaf);
        Assert.Empty(filters.Attributes);
        Assert.Equal(6, filters.Children.Count);
    }

    [Fact]
    public async Task Product_page_has_ordered_specs_images_and_category_path()
    {
        var product = await _client.GetFromJsonAsync<ProductDetailsDto>("/api/v1/products/by-slug/amd-ryzen-7-7800x3d", Ct);

        Assert.Equal("100-100000910WOF", product!.ModelNumber);
        Assert.Equal(["components", "cpu"], product.CategoryPath.Select(c => c.Slug));
        Assert.Equal("Socket", product.Specs[0].Name);
        Assert.Equal("AM5", product.Specs[0].DisplayValue);
        Assert.Equal("4.2 GHz", product.Specs.Single(s => s.Slug == "base-clock").DisplayValue);
        Assert.Equal("120 W", product.Specs.Single(s => s.Slug == "tdp").DisplayValue);
        Assert.Single(product.Images);
        Assert.Empty(product.Offers);

        var byId = await _client.GetFromJsonAsync<ProductDetailsDto>($"/api/v1/products/{product.Id}", Ct);
        Assert.Equal(product.Slug, byId!.Slug);
    }

    [Fact]
    public async Task Inactive_and_unknown_products_return_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/products/by-slug/intel-core-i9-13900k", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/v1/products/999999", Ct)).StatusCode);
    }

    [Fact]
    public async Task Category_attributes_include_option_ids()
    {
        var tree = await _client.GetFromJsonAsync<List<CategoryTreeDto>>("/api/v1/categories", Ct);
        var ram = tree!.Single(c => c.Slug == "components").Children.Single(c => c.Slug == "ram");

        var attributes = await _client.GetFromJsonAsync<List<SpecAttributeDto>>($"/api/v1/categories/{ram.Id}/attributes", Ct);

        Assert.Equal(6, attributes!.Count);
        Assert.Equal(["DDR4", "DDR5"], attributes.Single(a => a.Slug == "memory-type").Options.Select(o => o.Value));
    }
}
