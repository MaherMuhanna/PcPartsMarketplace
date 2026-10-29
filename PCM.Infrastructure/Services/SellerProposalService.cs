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

public sealed class SellerProposalService(
    IUnitOfWork unitOfWork,
    ProductEditor productEditor,
    ILogger<SellerProposalService> logger) : ISellerProposalService
{
    public async Task<PagedResponse<ProductManageListItemDto>> GetProposalsAsync(
        int userId, SellerProposalListQuery query, CancellationToken cancellationToken = default)
    {
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);
        var filter = new ProductManageFilter
        {
            Status = ProductAdminService.NormalizeStatus(query.Status),
            ProposedBySellerId = sellerId
        };

        var page = await unitOfWork.Products.GetManagePagedAsync(
            filter, query.Page, query.PageSize, ProductAdminService.ParseManageSort(query.Sort), cancellationToken);
        return page.ToResponse(p => p.ToManageListItemDto());
    }

    public async Task<ProductManageDto> GetProposalAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);
        return (await GetOwnedAsync(sellerId, id, cancellationToken)).ToManageDto();
    }

    public async Task<ProductManageDto> CreateAsync(int userId, ProductUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);
        var product = new Product
        {
            Status = nameof(ProductStatus.Pending),
            ProposedBySellerId = sellerId,
            IsActive = true     // becomes visible as soon as an admin approves it
        };

        await productEditor.ApplyAsync(product, request, cancellationToken);
        await unitOfWork.Products.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seller {SellerId} proposed product {ProductId} ({Slug})", sellerId, product.Id, product.Slug);
        return await GetProposalAsync(userId, product.Id, cancellationToken);
    }

    public async Task<ProductManageDto> UpdateAsync(int userId, int id, ProductUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);
        var product = await GetOwnedAsync(sellerId, id, cancellationToken);

        if (product.Status == nameof(ProductStatus.Approved))
        {
            throw new ConflictException("This product is approved and part of the catalog; only an admin can change it now.");
        }

        await productEditor.ApplyAsync(product, request, cancellationToken);

        // A fixed rejected proposal goes back into the review queue.
        product.Status = nameof(ProductStatus.Pending);
        product.ReviewNote = null;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetProposalAsync(userId, product.Id, cancellationToken);
    }

    // ---------------------------------------------------------------------

    private async Task<int> GetSellerIdAsync(int userId, CancellationToken cancellationToken)
    {
        // The ApprovedSeller policy has already checked the profile; this only maps user -> seller id.
        var profile = await unitOfWork.SellerProfiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Seller profile was not found.");
        return profile.Id;
    }

    /// <summary>The seller's own proposal, or 404 (also when it belongs to someone else).</summary>
    private async Task<Product> GetOwnedAsync(int sellerId, int id, CancellationToken cancellationToken)
    {
        var product = await unitOfWork.Products.GetForEditAsync(id, cancellationToken);
        return product is not null && product.ProposedBySellerId == sellerId
            ? product
            : throw new NotFoundException($"Proposal {id} was not found.");
    }
}
