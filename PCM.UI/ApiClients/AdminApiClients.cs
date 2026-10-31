using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Dashboards;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.UI.ApiClients;

/// <summary>Builds "a=1&amp;b=x" from the values that are set (escaped), for list endpoints.</summary>
internal static class QueryString
{
    public static string Build(params (string Name, object? Value)[] parameters) =>
        string.Join('&', parameters
            .Where(p => p.Value is not null && !(p.Value is string s && string.IsNullOrWhiteSpace(s)))
            .Select(p => $"{p.Name}={Uri.EscapeDataString(Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)!.Trim())}"));
}

// ---------------------------------------------------------------------
// Dashboards
// ---------------------------------------------------------------------

public interface IDashboardApiClient
{
    Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default);

    Task<SellerDashboardDto> GetSellerDashboardAsync(CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/admin/dashboard (admins) and /api/v1/seller/dashboard (approved sellers).</summary>
public sealed class DashboardApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IDashboardApiClient
{
    public Task<AdminDashboardDto> GetAdminDashboardAsync(CancellationToken cancellationToken = default) =>
        GetAsync<AdminDashboardDto>("api/v1/admin/dashboard", cancellationToken);

    public Task<SellerDashboardDto> GetSellerDashboardAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SellerDashboardDto>("api/v1/seller/dashboard", cancellationToken);
}

// ---------------------------------------------------------------------
// Categories, attributes, options and brands
// ---------------------------------------------------------------------

public interface IAdminCatalogApiClient
{
    /// <summary>Every attribute of a category with its options (public endpoint, used by forms).</summary>
    Task<IReadOnlyList<SpecAttributeDto>> GetAttributesAsync(int categoryId, CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    Task<CategoryDto> UpdateCategoryAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default);

    Task<SpecAttributeDto> CreateAttributeAsync(int categoryId, SpecAttributeRequest request, CancellationToken cancellationToken = default);

    Task<SpecAttributeDto> UpdateAttributeAsync(int categoryId, int attributeId, SpecAttributeRequest request, CancellationToken cancellationToken = default);

    Task DeleteAttributeAsync(int categoryId, int attributeId, CancellationToken cancellationToken = default);

    Task<SpecOptionDto> CreateOptionAsync(int attributeId, SpecOptionRequest request, CancellationToken cancellationToken = default);

    Task<SpecOptionDto> UpdateOptionAsync(int attributeId, int optionId, SpecOptionRequest request, CancellationToken cancellationToken = default);

    Task DeleteOptionAsync(int attributeId, int optionId, CancellationToken cancellationToken = default);

    /// <summary>Public brand list (one page, sorted by name unless <paramref name="sort"/> says otherwise).</summary>
    Task<PagedResponse<BrandDto>> GetBrandsAsync(int page, int pageSize, string? sort = null, CancellationToken cancellationToken = default);

    Task<BrandDto> CreateBrandAsync(BrandRequest request, CancellationToken cancellationToken = default);

    Task<BrandDto> UpdateBrandAsync(int id, BrandRequest request, CancellationToken cancellationToken = default);

    Task DeleteBrandAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for the admin catalog structure endpoints (admins only; others get 403).</summary>
public sealed class AdminCatalogApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IAdminCatalogApiClient
{
    private const string Categories = "api/v1/admin/categories";
    private const string Attributes = "api/v1/admin/attributes";
    private const string Brands = "api/v1/admin/brands";

    public Task<IReadOnlyList<SpecAttributeDto>> GetAttributesAsync(int categoryId, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<SpecAttributeDto>>($"api/v1/categories/{categoryId}/attributes", cancellationToken);

    public Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CategoryDto>(HttpMethod.Post, Categories, request, cancellationToken);

    public Task<CategoryDto> UpdateCategoryAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"{Categories}/{id}", request, cancellationToken);

    public Task DeleteCategoryAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Categories}/{id}", body: null, cancellationToken);

    public Task<SpecAttributeDto> CreateAttributeAsync(int categoryId, SpecAttributeRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SpecAttributeDto>(HttpMethod.Post, $"{Categories}/{categoryId}/attributes", request, cancellationToken);

    public Task<SpecAttributeDto> UpdateAttributeAsync(int categoryId, int attributeId, SpecAttributeRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SpecAttributeDto>(HttpMethod.Put, $"{Categories}/{categoryId}/attributes/{attributeId}", request, cancellationToken);

    public Task DeleteAttributeAsync(int categoryId, int attributeId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Categories}/{categoryId}/attributes/{attributeId}", body: null, cancellationToken);

    public Task<SpecOptionDto> CreateOptionAsync(int attributeId, SpecOptionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SpecOptionDto>(HttpMethod.Post, $"{Attributes}/{attributeId}/options", request, cancellationToken);

    public Task<SpecOptionDto> UpdateOptionAsync(int attributeId, int optionId, SpecOptionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SpecOptionDto>(HttpMethod.Put, $"{Attributes}/{attributeId}/options/{optionId}", request, cancellationToken);

    public Task DeleteOptionAsync(int attributeId, int optionId, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Attributes}/{attributeId}/options/{optionId}", body: null, cancellationToken);

    public Task<PagedResponse<BrandDto>> GetBrandsAsync(int page, int pageSize, string? sort = null, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResponse<BrandDto>>($"api/v1/brands?{QueryString.Build(("page", page), ("pageSize", pageSize), ("sort", sort))}", cancellationToken);

    public Task<BrandDto> CreateBrandAsync(BrandRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<BrandDto>(HttpMethod.Post, Brands, request, cancellationToken);

    public Task<BrandDto> UpdateBrandAsync(int id, BrandRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<BrandDto>(HttpMethod.Put, $"{Brands}/{id}", request, cancellationToken);

    public Task DeleteBrandAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Delete, $"{Brands}/{id}", body: null, cancellationToken);
}

// ---------------------------------------------------------------------
// Products: admin management and seller proposals share the same shapes
// ---------------------------------------------------------------------

/// <summary>Filters of a product management list (admin products or seller proposals).</summary>
public sealed record ProductListFilter(string? Status, int? CategoryId, string? Q, string? Sort, int Page, int PageSize);

/// <summary>What the shared product form needs: load one, list, create, update. Implemented for admins and for sellers.</summary>
public interface IProductEditorApiClient
{
    Task<PagedResponse<ProductManageListItemDto>> GetProductsAsync(ProductListFilter filter, CancellationToken cancellationToken = default);

    Task<ProductManageDto> GetProductAsync(int id, CancellationToken cancellationToken = default);

    Task<ProductManageDto> CreateAsync(ProductUpsertRequest request, CancellationToken cancellationToken = default);

    Task<ProductManageDto> UpdateAsync(int id, ProductUpsertRequest request, CancellationToken cancellationToken = default);
}

public interface IAdminProductsApiClient : IProductEditorApiClient
{
    Task<ProductManageDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

    Task<ProductManageDto> ApproveAsync(int id, CancellationToken cancellationToken = default);

    Task<ProductManageDto> RejectAsync(int id, string note, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/admin/products (admins only).</summary>
public sealed class AdminProductsApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IAdminProductsApiClient
{
    private const string BasePath = "api/v1/admin/products";

    public Task<PagedResponse<ProductManageListItemDto>> GetProductsAsync(ProductListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var query = QueryString.Build(
            ("page", filter.Page), ("pageSize", filter.PageSize), ("status", filter.Status),
            ("categoryId", filter.CategoryId), ("q", filter.Q), ("sort", filter.Sort));
        return GetAsync<PagedResponse<ProductManageListItemDto>>($"{BasePath}?{query}", cancellationToken);
    }

    public Task<ProductManageDto> GetProductAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<ProductManageDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ProductManageDto> CreateAsync(ProductUpsertRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProductManageDto>(HttpMethod.Post, BasePath, request, cancellationToken);

    public Task<ProductManageDto> UpdateAsync(int id, ProductUpsertRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProductManageDto>(HttpMethod.Put, $"{BasePath}/{id}", request, cancellationToken);

    public Task<ProductManageDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default) =>
        SendAsync<ProductManageDto>(HttpMethod.Post, $"{BasePath}/{id}/{(isActive ? "activate" : "deactivate")}", body: null, cancellationToken);

    public Task<ProductManageDto> ApproveAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync<ProductManageDto>(HttpMethod.Post, $"{BasePath}/{id}/approve", body: null, cancellationToken);

    public Task<ProductManageDto> RejectAsync(int id, string note, CancellationToken cancellationToken = default) =>
        SendAsync<ProductManageDto>(HttpMethod.Post, $"{BasePath}/{id}/reject", new ProductReviewRequest { Note = note }, cancellationToken);
}

public interface ISellerProposalsApiClient : IProductEditorApiClient;

/// <summary>Typed client for /api/v1/seller/products/proposals (approved sellers only; own proposals).</summary>
public sealed class SellerProposalsApiClient(HttpClient httpClient) : ApiClientBase(httpClient), ISellerProposalsApiClient
{
    private const string BasePath = "api/v1/seller/products/proposals";

    public Task<PagedResponse<ProductManageListItemDto>> GetProductsAsync(ProductListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        // The seller endpoint has no category or keyword filter.
        var query = QueryString.Build(("page", filter.Page), ("pageSize", filter.PageSize), ("status", filter.Status), ("sort", filter.Sort));
        return GetAsync<PagedResponse<ProductManageListItemDto>>($"{BasePath}?{query}", cancellationToken);
    }

    public Task<ProductManageDto> GetProductAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<ProductManageDto>($"{BasePath}/{id}", cancellationToken);

    public Task<ProductManageDto> CreateAsync(ProductUpsertRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProductManageDto>(HttpMethod.Post, BasePath, request, cancellationToken);

    public Task<ProductManageDto> UpdateAsync(int id, ProductUpsertRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProductManageDto>(HttpMethod.Put, $"{BasePath}/{id}", request, cancellationToken);
}

// ---------------------------------------------------------------------
// Admin orders
// ---------------------------------------------------------------------

public interface IAdminOrdersApiClient
{
    /// <summary>Q = part of the order number; status = any sub-order in that status.</summary>
    Task<PagedResponse<OrderListItemDto>> GetOrdersAsync(string? status, string? q, string? sort, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<OrderDto> GetOrderAsync(int id, CancellationToken cancellationToken = default);

    Task<OrderDto> ChangeStatusAsync(int id, int sellerOrderId, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/admin/orders (admins only).</summary>
public sealed class AdminOrdersApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IAdminOrdersApiClient
{
    private const string BasePath = "api/v1/admin/orders";

    public Task<PagedResponse<OrderListItemDto>> GetOrdersAsync(string? status, string? q, string? sort, int page, int pageSize, CancellationToken cancellationToken = default) =>
        GetAsync<PagedResponse<OrderListItemDto>>(
            $"{BasePath}?{QueryString.Build(("page", page), ("pageSize", pageSize), ("status", status), ("q", q), ("sort", sort))}",
            cancellationToken);

    public Task<OrderDto> GetOrderAsync(int id, CancellationToken cancellationToken = default) =>
        GetAsync<OrderDto>($"{BasePath}/{id}", cancellationToken);

    public Task<OrderDto> ChangeStatusAsync(int id, int sellerOrderId, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<OrderDto>(HttpMethod.Patch, $"{BasePath}/{id}/seller-orders/{sellerOrderId}/status", request, cancellationToken);
}
