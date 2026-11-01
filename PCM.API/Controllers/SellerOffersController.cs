using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Caching;
using PCM.API.Security;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// The signed-in seller's offers and inventory. Approved sellers only (checked in the database on
/// every request). The seller comes from the token; other sellers' offers return 404.
/// </summary>
[ApiController]
[Route("api/v1/seller/offers")]
[Authorize(Policy = Policies.ApprovedSeller)]
[InvalidatesCatalogCache(CatalogCacheScope.Products)]
public sealed class SellerOffersController(ISellerOfferService offerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<OfferDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OfferDto>>> GetOffers(
        [FromQuery] SellerOfferListQuery query, CancellationToken cancellationToken) =>
        Ok(await offerService.GetOffersAsync(User.GetUserId(), query, cancellationToken));

    /// <summary>Offers whose available stock is at or below their low-stock threshold (lowest first by default). The stock parameter is ignored.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType<PagedResponse<OfferDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<OfferDto>>> GetLowStock(
        [FromQuery] SellerOfferListQuery query, CancellationToken cancellationToken) =>
        Ok(await offerService.GetLowStockAsync(User.GetUserId(), query, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<OfferDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OfferDto>> GetOffer(int id, CancellationToken cancellationToken) =>
        Ok(await offerService.GetOfferAsync(User.GetUserId(), id, cancellationToken));

    /// <summary>Offers a published product. One offer per product and condition.</summary>
    [HttpPost]
    [ProducesResponseType<OfferDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OfferDto>> Create(CreateOfferRequest request, CancellationToken cancellationToken)
    {
        var offer = await offerService.CreateAsync(User.GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetOffer), new { id = offer.Id }, offer);
    }

    /// <summary>Changes price and low-stock threshold. Stock changes use PATCH .../stock.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<OfferDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OfferDto>> Update(int id, UpdateOfferRequest request, CancellationToken cancellationToken) =>
        Ok(await offerService.UpdateAsync(User.GetUserId(), id, request, cancellationToken));

    /// <summary>Restock (add) or Adjustment (add or remove, note required). Recorded as a stock movement.</summary>
    [HttpPatch("{id:int}/stock")]
    [ProducesResponseType<OfferDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OfferDto>> ChangeStock(int id, StockChangeRequest request, CancellationToken cancellationToken) =>
        Ok(await offerService.ChangeStockAsync(User.GetUserId(), id, request, cancellationToken));

    /// <summary>Removes the offer from sale (soft delete). 409 while open orders hold reserved stock.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await offerService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:int}/movements")]
    [ProducesResponseType<PagedResponse<StockMovementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<StockMovementDto>>> GetMovements(
        int id, [FromQuery] StockMovementListQuery query, CancellationToken cancellationToken) =>
        Ok(await offerService.GetMovementsAsync(User.GetUserId(), id, query, cancellationToken));
}
