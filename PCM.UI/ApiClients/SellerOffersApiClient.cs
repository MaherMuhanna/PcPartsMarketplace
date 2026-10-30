using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Offers;

namespace PCM.UI.ApiClients;

/// <summary>Filters of the "My offers" list, sent as the API's query string.</summary>
public sealed record OfferListFilter(string? Q, string? Condition, string? Stock, string? Sort, int Page, int PageSize);

public interface ISellerOffersApiClient
{
    Task<PagedResponse<OfferDto>> GetOffersAsync(OfferListFilter filter, CancellationToken cancellationToken = default);

    Task<OfferDto> GetOfferAsync(int id, CancellationToken cancellationToken = default);

    Task<OfferDto> CreateAsync(CreateOfferRequest request, CancellationToken cancellationToken = default);

    Task<OfferDto> UpdateAsync(int id, UpdateOfferRequest request, CancellationToken cancellationToken = default);

    Task<OfferDto> ChangeStockAsync(int id, StockChangeRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResponse<StockMovementDto>> GetMovementsAsync(int id, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/seller/offers (approved sellers only; others get 403).</summary>
public sealed class SellerOffersApiClient(HttpClient httpClient) : ApiClientBase(httpClient), ISellerOffersApiClient
{
    private const string BasePath = "api/v1/seller/offers";

    public Task<PagedResponse<OfferDto>> GetOffersAsync(OfferListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var parameters = new List<string> { $"page={filter.Page}", $"pageSize={filter.PageSize}" };
        AddIfSet(parameters, "q", filter.Q);
        AddIfSet(parameters, "condition", filter.Condition);
        AddIfSet(parameters, "stock", filter.Stock);
        AddIfSet(parameters, "sort", filter.Sort);

        return GetAsync<PagedResponse<OfferDto>>($"{BasePath}?{string.Join('&', parameters)}", cancellationToken);
    }

    public Task<OfferDto> GetOfferAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<OfferDto>($"{BasePath}/{id}", cancellationToken);

    public Task<OfferDto> CreateAsync(CreateOfferRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OfferDto>(HttpMethod.Post, BasePath, request, cancellationToken);

    public Task<OfferDto> UpdateAsync(int id, UpdateOfferRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OfferDto>(HttpMethod.Put, $"{BasePath}/{id}", request, cancellationToken);

    public Task<OfferDto> ChangeStockAsync(int id, StockChangeRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OfferDto>(HttpMethod.Patch, $"{BasePath}/{id}/stock", request, cancellationToken);

    public Task DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{BasePath}/{id}", body: null, cancellationToken);

    public Task<PagedResponse<StockMovementDto>> GetMovementsAsync(int id, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResponse<StockMovementDto>>($"{BasePath}/{id}/movements?page={page}&pageSize={pageSize}", cancellationToken);

    private static void AddIfSet(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }
}
