using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Security;
using PCM.Infrastructure.Services;

namespace PCM.API.Controllers;

/// <summary>
/// Category tree and per-category spec attributes (admins only).
/// Products and attributes belong to leaf categories; rule violations return 409.
/// </summary>
[ApiController]
[Route("api/v1/admin/categories")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class AdminCategoriesController(
    ICategoryService categoryService,
    ISpecAttributeService attributeService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/categories/{category.Id}/attributes", category);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Update(int id, UpdateCategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await categoryService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await categoryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Adds an attribute to a leaf category. The slug (filter key) is generated from the name.</summary>
    [HttpPost("{id:int}/attributes")]
    [ProducesResponseType<SpecAttributeDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpecAttributeDto>> CreateAttribute(int id, SpecAttributeRequest request, CancellationToken cancellationToken)
    {
        var attribute = await attributeService.CreateAsync(id, request, cancellationToken);
        return Created($"/api/v1/categories/{id}/attributes", attribute);
    }

    [HttpPut("{id:int}/attributes/{attributeId:int}")]
    [ProducesResponseType<SpecAttributeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SpecAttributeDto>> UpdateAttribute(
        int id, int attributeId, SpecAttributeRequest request, CancellationToken cancellationToken) =>
        Ok(await attributeService.UpdateAsync(id, attributeId, request, cancellationToken));

    [HttpDelete("{id:int}/attributes/{attributeId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAttribute(int id, int attributeId, CancellationToken cancellationToken)
    {
        await attributeService.DeleteAsync(id, attributeId, cancellationToken);
        return NoContent();
    }
}
