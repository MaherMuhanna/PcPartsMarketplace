using System.Net.Http.Json;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;

namespace PCM.UI.ApiClients;

public interface IBrandsApiClient
{
    Task<PagedResponse<BrandDto>?> GetBrandsAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/brands. The HttpClient is created and pooled by IHttpClientFactory.</summary>
public sealed class BrandsApiClient(HttpClient httpClient) : IBrandsApiClient
{
    public async Task<PagedResponse<BrandDto>?> GetBrandsAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/v1/brands?page={page}&pageSize={pageSize}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResponse<BrandDto>>(cancellationToken);
    }
}
