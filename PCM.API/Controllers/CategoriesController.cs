using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>Public, read-only category tree and category attributes.</summary>
[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    /// <summary>The whole tree. Products attach to leaves (isLeaf = true).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryTreeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryTreeDto>>> GetTree(CancellationToken cancellationToken) =>
        Ok(await categoryService.GetTreeAsync(cancellationToken));

    /// <summary>
    /// Every attribute of a category with its options (ids included), e.g. to build a product form.
    /// Empty for a non-leaf category.
    /// </summary>
    [HttpGet("{id:int}/attributes")]
    [ProducesResponseType<IReadOnlyList<SpecAttributeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<SpecAttributeDto>>> GetAttributes(int id, CancellationToken cancellationToken) =>
        Ok(await categoryService.GetAttributesAsync(id, cancellationToken));
}
