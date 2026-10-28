using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// The signed-in seller's product proposals. Approved sellers only (checked in the database on
/// every request). The seller comes from the token; other sellers' proposals return 404.
/// </summary>
[ApiController]
[Route("api/v1/seller/products/proposals")]
[Authorize(Policy = Policies.ApprovedSeller)]
public sealed class SellerProposalsController(ISellerProposalService proposalService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<ProductManageListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<ProductManageListItemDto>>> GetProposals(
        [FromQuery] SellerProposalListQuery query, CancellationToken cancellationToken) =>
        Ok(await proposalService.GetProposalsAsync(User.GetUserId(), query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductManageDto>> GetProposal(int id, CancellationToken cancellationToken) =>
        Ok(await proposalService.GetProposalAsync(User.GetUserId(), id, cancellationToken));

    /// <summary>Proposes a new master product. It stays Pending until an admin reviews it.</summary>
    [HttpPost]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductManageDto>> Create(ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var proposal = await proposalService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetProposal), new { id = proposal.Id }, proposal);
    }

    /// <summary>Edits a Pending or Rejected proposal (a rejected one goes back to Pending). 409 once approved.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ProductManageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductManageDto>> Update(int id, ProductUpsertRequest request, CancellationToken cancellationToken) =>
        Ok(await proposalService.UpdateAsync(User.GetUserId(), id, request, cancellationToken));
}
