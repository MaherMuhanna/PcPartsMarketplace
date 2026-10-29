using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Brand management (admins only). The public list is GET /api/v1/brands.</summary>
[ApiController]
[Route("api/v1/admin/brands")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminBrandsController(IBrandService brandService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<BrandDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BrandDto>> Create(BrandRequest request, CancellationToken cancellationToken)
    {
        var brand = await brandService.CreateAsync(request, cancellationToken);
        return Created("/api/v1/brands?sort=newest", brand);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<BrandDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BrandDto>> Update(int id, BrandRequest request, CancellationToken cancellationToken) =>
        Ok(await brandService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Soft-deletes a brand. 409 while products use it.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await brandService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
