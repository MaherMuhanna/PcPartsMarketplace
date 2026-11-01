using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Caching;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// Master products in any status (admins only): create, edit, show/hide, and review seller proposals
/// (Pending -> Approved | Rejected).
/// </summary>
[ApiController]
[Route("api/v1/admin/products")]
[Authorize(Policy = Policies.AdminOnly)]
[InvalidatesCatalogCache(CatalogCacheScope.Products)]
public sealed class AdminProductsController(IProductAdminService productAdminService) : ControllerBase
{
    /// <summary>Lists products. Filter by status (Pending = review queue), categoryId or keyword q.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductManageListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ProductManageListItemDto>>> GetProducts(
        [FromQuery] AdminProductListQuery query, CancellationToken cancellationToken) =>
        Ok(await productAdminService.GetProductsAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductManageDto>> GetProduct(int id, CancellationToken cancellationToken) =>
        Ok(await productAdminService.GetProductAsync(id, cancellationToken));

    /// <summary>Creates an approved, active product. Specs must fit the category's attributes.</summary>
    [HttpPost]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductManageDto>> Create(ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var product = await productAdminService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    /// <summary>Replaces the product's data, specs and images. The status does not change.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductManageDto>> Update(int id, ProductUpsertRequest request, CancellationToken cancellationToken) =>
        Ok(await productAdminService.UpdateAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/activate")]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductManageDto>> Activate(int id, CancellationToken cancellationToken) =>
        Ok(await productAdminService.SetActiveAsync(id, isActive: true, cancellationToken));

    /// <summary>Hides the product from the catalog without deleting it.</summary>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductManageDto>> Deactivate(int id, CancellationToken cancellationToken) =>
        Ok(await productAdminService.SetActiveAsync(id, isActive: false, cancellationToken));

    [HttpPost("{id:int}/approve")]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductManageDto>> Approve(int id, CancellationToken cancellationToken) =>
        Ok(await productAdminService.ApproveAsync(id, cancellationToken));

    /// <summary>Rejects a pending proposal. The note is shown to the seller, who can fix and resubmit.</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductManageDto>> Reject(int id, ProductReviewRequest request, CancellationToken cancellationToken) =>
        Ok(await productAdminService.RejectAsync(id, request.Note, cancellationToken));
}
