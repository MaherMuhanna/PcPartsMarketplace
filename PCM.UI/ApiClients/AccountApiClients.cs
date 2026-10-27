using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Sellers;

namespace PCM.UI.ApiClients;

public interface IAddressesApiClient
{
    Task<PagedResponse<AddressDto>> GetAddressesAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<AddressDto> GetAddressAsync(int id, CancellationToken cancellationToken = default);

    Task<AddressDto> CreateAddressAsync(CreateAddressRequest request, CancellationToken cancellationToken = default);

    Task<AddressDto> UpdateAddressAsync(int id, UpdateAddressRequest request, CancellationToken cancellationToken = default);

    Task DeleteAddressAsync(int id, CancellationToken cancellationToken = default);

    Task SetDefaultAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/account/addresses (customer only).</summary>
public sealed class AddressesApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IAddressesApiClient
{
    private const string BasePath = "api/v1/account/addresses";

    public Task<PagedResponse<AddressDto>> GetAddressesAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResponse<AddressDto>>($"{BasePath}?page={page}&pageSize={pageSize}", cancellationToken);

    public Task<AddressDto> GetAddressAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<AddressDto>($"{BasePath}/{id}", cancellationToken);

    public Task<AddressDto> CreateAddressAsync(CreateAddressRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AddressDto>(HttpMethod.Post, BasePath, request, cancellationToken);

    public Task<AddressDto> UpdateAddressAsync(int id, UpdateAddressRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AddressDto>(HttpMethod.Put, $"{BasePath}/{id}", request, cancellationToken);

    public Task DeleteAddressAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{BasePath}/{id}", body: null, cancellationToken);

    public Task SetDefaultAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"{BasePath}/{id}/default", body: null, cancellationToken);
}

public interface IAdminSellersApiClient
{
    Task<PagedResponse<SellerAdminDto>> GetSellersAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<SellerAdminDto> ApproveAsync(int id, CancellationToken cancellationToken = default);

    Task<SellerAdminDto> RejectAsync(int id, string reason, CancellationToken cancellationToken = default);

    Task<SellerAdminDto> SuspendAsync(int id, string reason, CancellationToken cancellationToken = default);

    Task<SellerAdminDto> ReinstateAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/admin/sellers (admin only).</summary>
public sealed class AdminSellersApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IAdminSellersApiClient
{
    private const string BasePath = "api/v1/admin/sellers";

    public Task<PagedResponse<SellerAdminDto>> GetSellersAsync(string? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var url = $"{BasePath}?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(status))
        {
            url += $"&status={Uri.EscapeDataString(status)}";
        }

        return GetAsync<PagedResponse<SellerAdminDto>>(url, cancellationToken);
    }

    public Task<SellerAdminDto> ApproveAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync<SellerAdminDto>(HttpMethod.Post, $"{BasePath}/{id}/approve", body: null, cancellationToken);

    public Task<SellerAdminDto> RejectAsync(int id, string reason, CancellationToken cancellationToken = default) =>
        SendAsync<SellerAdminDto>(HttpMethod.Post, $"{BasePath}/{id}/reject", new SellerStatusReasonRequest { Reason = reason }, cancellationToken);

    public Task<SellerAdminDto> SuspendAsync(int id, string reason, CancellationToken cancellationToken = default) =>
        SendAsync<SellerAdminDto>(HttpMethod.Post, $"{BasePath}/{id}/suspend", new SellerStatusReasonRequest { Reason = reason }, cancellationToken);

    public Task<SellerAdminDto> ReinstateAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync<SellerAdminDto>(HttpMethod.Post, $"{BasePath}/{id}/reinstate", body: null, cancellationToken);
}

public interface ISellerProfileApiClient
{
    Task<SellerProfileDto> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<SellerProfileDto> UpdateProfileAsync(UpdateSellerProfileRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/seller/profile (any seller).</summary>
public sealed class SellerProfileApiClient(HttpClient httpClient) : ApiClientBase(httpClient), ISellerProfileApiClient
{
    private const string BasePath = "api/v1/seller/profile";

    public Task<SellerProfileDto> GetProfileAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SellerProfileDto>(BasePath, cancellationToken);

    public Task<SellerProfileDto> UpdateProfileAsync(UpdateSellerProfileRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SellerProfileDto>(HttpMethod.Put, BasePath, request, cancellationToken);
}
