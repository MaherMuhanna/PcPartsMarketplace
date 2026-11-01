using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.API.Caching;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Allowed values of Enum attributes (admins only). An option used by products cannot be deleted (409).</summary>
[ApiController]
[Route("api/v1/admin/attributes/{attributeId:int}/options")]
[Authorize(Policy = Policies.AdminOnly)]
[InvalidatesCatalogCache(CatalogCacheScope.All)]
public sealed class AdminSpecOptionsController(ISpecAttributeService attributeService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SpecOptionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpecOptionDto>> Create(int attributeId, SpecOptionRequest request, CancellationToken cancellationToken)
    {
        var option = await attributeService.CreateOptionAsync(attributeId, request, cancellationToken);
        return Created($"/api/v1/admin/attributes/{attributeId}/options/{option.Id}", option);
    }

    [HttpPut("{optionId:int}")]
    [ProducesResponseType<SpecOptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpecOptionDto>> Update(int attributeId, int optionId, SpecOptionRequest request, CancellationToken cancellationToken) =>
        Ok(await attributeService.UpdateOptionAsync(attributeId, optionId, request, cancellationToken));

    [HttpDelete("{optionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int attributeId, int optionId, CancellationToken cancellationToken)
    {
        await attributeService.DeleteOptionAsync(attributeId, optionId, cancellationToken);
        return NoContent();
    }
}
