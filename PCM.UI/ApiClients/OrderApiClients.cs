using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.UI.ApiClients;

public interface ICartApiClient
{
    Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default);

    Task<CartSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default);

    Task<CartDto> UpdateItemAsync(int id, UpdateCartItemRequest request, CancellationToken cancellationToken = default);

    Task RemoveItemAsync(int id, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/cart (customers only; others get 403).</summary>
public sealed class CartApiClient(HttpClient httpClient) : ApiClientBase(httpClient), ICartApiClient
{
    private const string BasePath = "api/v1/cart";

    public Task<CartDto> GetCartAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CartDto>(BasePath, cancellationToken);

    public Task<CartSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CartSummaryDto>($"{BasePath}/summary", cancellationToken);

    public Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CartDto>(HttpMethod.Post, $"{BasePath}/items", request, cancellationToken);

    public Task<CartDto> UpdateItemAsync(int id, UpdateCartItemRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CartDto>(HttpMethod.Put, $"{BasePath}/items/{id}", request, cancellationToken);

    public Task RemoveItemAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{BasePath}/items/{id}", body: null, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, BasePath, body: null, cancellationToken);
}

public interface IOrdersApiClient
{
    Task<OrderDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    Task<PagedResponse<OrderListItemDto>> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<OrderDto> GetOrderAsync(int id, CancellationToken cancellationToken = default);

    Task<OrderDto> CancelOrderAsync(int id, CancelOrderRequest request, CancellationToken cancellationToken = default);

    Task<OrderDto> CancelSellerOrderAsync(int id, int sellerOrderId, CancelOrderRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/orders (customers only).</summary>
public sealed class OrdersApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IOrdersApiClient
{
    private const string BasePath = "api/v1/orders";

    public Task<OrderDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"{BasePath}/checkout", request, cancellationToken);

    public Task<PagedResponse<OrderListItemDto>> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResponse<OrderListItemDto>>($"{BasePath}?page={page}&pageSize={pageSize}", cancellationToken);

    public Task<OrderDto> GetOrderAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<OrderDto>($"{BasePath}/{id}", cancellationToken);

    public Task<OrderDto> CancelOrderAsync(int id, CancelOrderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"{BasePath}/{id}/cancel", request, cancellationToken);

    public Task<OrderDto> CancelSellerOrderAsync(int id, int sellerOrderId, CancelOrderRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OrderDto>(HttpMethod.Post, $"{BasePath}/{id}/seller-orders/{sellerOrderId}/cancel", request, cancellationToken);
}

public interface ISellerOrdersApiClient
{
    Task<PagedResponse<SellerOrderListItemDto>> GetOrdersAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<SellerOrderDetailsDto> GetOrderAsync(int id, CancellationToken cancellationToken = default);

    Task<SellerOrderDetailsDto> ChangeStatusAsync(int id, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/seller/orders (approved sellers only).</summary>
public sealed class SellerOrdersApiClient(HttpClient httpClient) : ApiClientBase(httpClient), ISellerOrdersApiClient
{
    private const string BasePath = "api/v1/seller/orders";

    public Task<PagedResponse<SellerOrderListItemDto>> GetOrdersAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var url = $"{BasePath}?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(status))
        {
            url += $"&status={Uri.EscapeDataString(status.Trim())}";
        }

        return GetAsync<PagedResponse<SellerOrderListItemDto>>(url, cancellationToken);
    }

    public Task<SellerOrderDetailsDto> GetOrderAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<SellerOrderDetailsDto>($"{BasePath}/{id}", cancellationToken);

    public Task<SellerOrderDetailsDto> ChangeStatusAsync(int id, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SellerOrderDetailsDto>(HttpMethod.Patch, $"{BasePath}/{id}/status", request, cancellationToken);
}
