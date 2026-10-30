using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;

namespace PCM.UI.ApiClients;

public interface ICatalogApiClient
{
    Task<IReadOnlyList<CategoryTreeDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Sidebar data for a category slug (null = whole catalog).</summary>
    Task<CatalogFiltersDto> GetFiltersAsync(string? categorySlug, CancellationToken cancellationToken = default);

    /// <summary>Search with a canonical catalog query string (see <see cref="Catalog.CatalogQuery"/>), forwarded as is.</summary>
    Task<PagedResponse<ProductListItemDto>> SearchAsync(string queryString, CancellationToken cancellationToken = default);

    Task<ProductDetailsDto> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<ProductDetailsDto> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for the public catalog endpoints (anonymous, so no token handler).</summary>
public sealed class CatalogApiClient(HttpClient httpClient) : ApiClientBase(httpClient), ICatalogApiClient
{
    public Task<IReadOnlyList<CategoryTreeDto>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<CategoryTreeDto>>("api/v1/categories", cancellationToken);

    public Task<CatalogFiltersDto> GetFiltersAsync(string? categorySlug, CancellationToken cancellationToken = default) =>
        GetAsync<CatalogFiltersDto>(
            string.IsNullOrEmpty(categorySlug)
                ? "api/v1/products/filters"
                : $"api/v1/products/filters?category={Uri.EscapeDataString(categorySlug)}",
            cancellationToken);

    public Task<PagedResponse<ProductListItemDto>> SearchAsync(string queryString, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResponse<ProductListItemDto>>(
            string.IsNullOrEmpty(queryString) ? "api/v1/products" : $"api/v1/products?{queryString}",
            cancellationToken);

    public Task<ProductDetailsDto> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default) =>
        GetAsync<ProductDetailsDto>($"api/v1/products/by-slug/{Uri.EscapeDataString(slug)}", cancellationToken);

    public Task<ProductDetailsDto> GetProductByIdAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<ProductDetailsDto>($"api/v1/products/{id}", cancellationToken);
}
