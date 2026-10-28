using Microsoft.Extensions.Logging;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Catalog;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Services;

public sealed class ProductAdminService(
    IUnitOfWork unitOfWork,
    ProductEditor productEditor,
    ILogger<ProductAdminService> logger) : IProductAdminService
{
    public async Task<PagedResponse<ProductManageListItemDto>> GetProductsAsync(AdminProductListQuery query, CancellationToken cancellationToken = default)
    {
        var filter = new ProductManageFilter
        {
            Status = NormalizeStatus(query.Status),
            CategoryId = query.CategoryId,
            Keyword = query.Q
        };

        var page = await unitOfWork.Products.GetManagePagedAsync(filter, query.Page, query.PageSize, ParseManageSort(query.Sort), cancellationToken);
        return page.ToResponse(p => p.ToManageListItemDto());
    }

    public async Task<ProductManageDto> GetProductAsync(int id, CancellationToken cancellationToken = default) =>
        (await GetForEditAsync(id, cancellationToken)).ToManageDto();

    public async Task<ProductManageDto> CreateAsync(ProductUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Status = nameof(ProductStatus.Approved),
            IsActive = true
        };

        await productEditor.ApplyAsync(product, request, cancellationToken);
        await unitOfWork.Products.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Admin created product {ProductId} ({Slug})", product.Id, product.Slug);

        // Reload with brand, category and attribute names for the response.
        return await GetProductAsync(product.Id, cancellationToken);
    }

    public async Task<ProductManageDto> UpdateAsync(int id, ProductUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var product = await GetForEditAsync(id, cancellationToken);

        await productEditor.ApplyAsync(product, request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await ReloadAsync(product, cancellationToken);
    }

    public async Task<ProductManageDto> SetActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default)
    {
        var product = await GetForEditAsync(id, cancellationToken);

        product.IsActive = isActive;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return product.ToManageDto();
    }

    public Task<ProductManageDto> ApproveAsync(int id, CancellationToken cancellationToken = default) =>
        ReviewAsync(id, ProductStatus.Approved, note: null, cancellationToken);

    public Task<ProductManageDto> RejectAsync(int id, string note, CancellationToken cancellationToken = default) =>
        ReviewAsync(id, ProductStatus.Rejected, note.Trim(), cancellationToken);

    // ---------------------------------------------------------------------

    /// <summary>Only a pending proposal can be reviewed. A seller who fixes a rejected one sends it back to Pending.</summary>
    private async Task<ProductManageDto> ReviewAsync(int id, ProductStatus decision, string? note, CancellationToken cancellationToken)
    {
        var product = await GetForEditAsync(id, cancellationToken);

        if (product.Status != nameof(ProductStatus.Pending))
        {
            throw new ConflictException($"Only pending products can be reviewed; this one is {product.Status}.");
        }

        product.Status = decision.ToString();
        product.ReviewNote = note;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Product {ProductId} reviewed: {Decision}", product.Id, decision);
        return product.ToManageDto();
    }

    private async Task<Product> GetForEditAsync(int id, CancellationToken cancellationToken) =>
        await unitOfWork.Products.GetForEditAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Product {id} was not found.");

    /// <summary>
    /// After an edit, brand/category navigations and the attributes of new spec values may be
    /// stale or missing, so the response is built from a fresh read.
    /// </summary>
    private async Task<ProductManageDto> ReloadAsync(Product product, CancellationToken cancellationToken) =>
        await GetProductAsync(product.Id, cancellationToken);

    /// <summary>Canonical casing ("pending" -> Pending). The validator has already checked the value.</summary>
    internal static string? NormalizeStatus(string? status) =>
        status is null ? null : Enum.Parse<ProductStatus>(status, ignoreCase: true).ToString();

    internal static ProductManageSort ParseManageSort(string? sort) => sort?.ToLowerInvariant() switch
    {
        "oldest" => ProductManageSort.Oldest,
        "name" => ProductManageSort.Name,
        _ => ProductManageSort.Newest
    };
}
