using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PCM.API.Caching;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Catalog brands. Phase 1 exposes the paged list as an end-to-end pipeline check.</summary>
[ApiController]
[Route("api/v1/brands")]
[OutputCache(PolicyName = CachePolicies.Catalog)]
public sealed class BrandsController(IBrandService brandService) : ControllerBase
{
    /// <summary>Lists brands, paged and sorted.</summary>
    /// <param name="query">page (default 1), pageSize (1-100, default 20), sort: name | -name | newest.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    [HttpGet]
    [ProducesResponseType<PagedResponse<BrandDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<BrandDto>>> GetBrands(
        [FromQuery] BrandListQuery query,
        CancellationToken cancellationToken) =>
        Ok(await brandService.GetBrandsAsync(query, cancellationToken));
}
