using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;

namespace PCM.API.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class BrandsEndpointTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>Brands from Database/seed.sql. Other tests may add brands, so this is a lower bound.</summary>
    private const int SeededBrands = 17;

    [Fact]
    public async Task Get_brands_returns_seeded_brands_sorted_by_name()
    {
        var response = await _client.GetFromJsonAsync<PagedResponse<BrandDto>>(
            "/api/v1/brands?pageSize=100", TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.True(response.TotalCount >= SeededBrands);
        Assert.Contains(response.Items, b => b is { Name: "G.Skill", Slug: "g-skill" });
        Assert.Equal(response.Items.Select(b => b.Name).Order(StringComparer.OrdinalIgnoreCase), response.Items.Select(b => b.Name));
    }

    [Fact]
    public async Task Get_brands_pages_results()
    {
        var response = await _client.GetFromJsonAsync<PagedResponse<BrandDto>>(
            "/api/v1/brands?page=2&pageSize=3", TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(3, response.Items.Count);
        Assert.Equal(2, response.Page);
        Assert.True(response.TotalCount >= SeededBrands);
        Assert.Equal((int)Math.Ceiling(response.TotalCount / 3.0), response.TotalPages);
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("sort=price", "sort")]
    public async Task Invalid_query_returns_400_problem_with_field_name(string queryString, string expectedField)
    {
        var response = await _client.GetAsync($"/api/v1/brands?{queryString}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty(expectedField, out _));
    }

    [Fact]
    public async Task Every_response_carries_a_correlation_id()
    {
        var response = await _client.GetAsync("/api/v1/brands", TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.False(string.IsNullOrWhiteSpace(values.Single()));
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem_details()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
