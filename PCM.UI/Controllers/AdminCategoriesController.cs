using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Security;
using PCM.UI.ApiClients;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>
/// Admin > Categories and specs, on one page: the category tree on the left; the selected category on
/// the right (rename, delete, add a subcategory) and, for a leaf, its spec attributes and Enum options.
/// The API enforces the tree rules (products and attributes only on leaves; nothing in use is deleted)
/// and its 400/409 messages are shown as flash errors.
/// </summary>
[Authorize(Roles = Roles.Admin)]
[Route("admin/categories")]
public sealed class AdminCategoriesController(ICatalogApiClient catalogApi, IAdminCatalogApiClient adminApi) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? id, CancellationToken cancellationToken)
    {
        var tree = await catalogApi.GetCategoriesAsync(cancellationToken);
        var selected = id is { } categoryId ? tree.Find(categoryId) : null;

        // Attributes live on leaves only; a parent has none.
        var attributes = selected is { Node.IsLeaf: true }
            ? await adminApi.GetAttributesAsync(selected.Node.Id, cancellationToken)
            : [];

        return View(new AdminCategoriesViewModel { Tree = tree, Selected = selected, Attributes = attributes });
    }

    // ---- Categories ----

    [HttpPost("")]
    public async Task<IActionResult> Create(int? parentId, string? name, int sortOrder, CancellationToken cancellationToken)
    {
        try
        {
            var category = await adminApi.CreateCategoryAsync(
                new CreateCategoryRequest { ParentId = parentId, Name = name?.Trim() ?? string.Empty, SortOrder = sortOrder }, cancellationToken);
            TempData["Success"] = $"Category {category.Name} added.";
            return RedirectToAction(nameof(Index), new { id = category.Id });
        }
        catch (ApiException ex) when (IsFormError(ex))
        {
            TempData["Error"] = ex.FirstMessage;
            return RedirectToAction(nameof(Index), new { id = parentId });
        }
    }

    [HttpPost("{id:int}/edit")]
    public Task<IActionResult> Edit(int id, string? name, int sortOrder, CancellationToken cancellationToken) =>
        RunAsync(id, async () =>
        {
            var category = await adminApi.UpdateCategoryAsync(id, new UpdateCategoryRequest { Name = name?.Trim() ?? string.Empty, SortOrder = sortOrder }, cancellationToken);
            return $"Category {category.Name} saved.";
        });

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id, int? parentId, CancellationToken cancellationToken)
    {
        try
        {
            await adminApi.DeleteCategoryAsync(id, cancellationToken);
            TempData["Success"] = "Category deleted.";
            return RedirectToAction(nameof(Index), new { id = parentId });
        }
        catch (ApiException ex) when (IsFormError(ex))
        {
            // 409: it still has subcategories or products.
            TempData["Error"] = ex.FirstMessage;
            return RedirectToAction(nameof(Index), new { id });
        }
    }

    // ---- Attributes ----

    [HttpPost("{id:int}/attributes")]
    public Task<IActionResult> CreateAttribute(int id, AttributeFormModel form, CancellationToken cancellationToken) =>
        RunAsync(id, async () =>
        {
            var attribute = await adminApi.CreateAttributeAsync(id, form.ToRequest(), cancellationToken);
            return attribute.DataType == "Enum"
                ? $"Attribute {attribute.Name} added. Add its options next."
                : $"Attribute {attribute.Name} added.";
        });

    [HttpPost("{id:int}/attributes/{attributeId:int}/edit")]
    public Task<IActionResult> EditAttribute(int id, int attributeId, AttributeFormModel form, CancellationToken cancellationToken) =>
        RunAsync(id, async () =>
        {
            var attribute = await adminApi.UpdateAttributeAsync(id, attributeId, form.ToRequest(), cancellationToken);
            return $"Attribute {attribute.Name} saved.";
        });

    [HttpPost("{id:int}/attributes/{attributeId:int}/delete")]
    public Task<IActionResult> DeleteAttribute(int id, int attributeId, CancellationToken cancellationToken) =>
        RunAsync(id, async () =>
        {
            await adminApi.DeleteAttributeAsync(id, attributeId, cancellationToken);
            return "Attribute deleted.";
        });

    // ---- Options of Enum attributes ----

    [HttpPost("{id:int}/attributes/{attributeId:int}/options")]
    public Task<IActionResult> CreateOption(int id, int attributeId, string? value, int sortOrder, CancellationToken cancellationToken) =>
        RunAsync(id, async () =>
        {
            var option = await adminApi.CreateOptionAsync(attributeId, new SpecOptionRequest { Value = value?.Trim() ?? string.Empty, SortOrder = sortOrder }, cancellationToken);
            return $"Option {option.Value} added.";
        });

    [HttpPost("{id:int}/attributes/{attributeId:int}/options/{optionId:int}/edit")]
    public Task<IActionResult> EditOption(int id, int attributeId, int optionId, string? value, int sortOrder, CancellationToken cancellationToken) =>
        RunAsync(id, async () =>
        {
            var option = await adminApi.UpdateOptionAsync(attributeId, optionId, new SpecOptionRequest { Value = value?.Trim() ?? string.Empty, SortOrder = sortOrder }, cancellationToken);
            return $"Option {option.Value} saved.";
        });

    [HttpPost("{id:int}/attributes/{attributeId:int}/options/{optionId:int}/delete")]
    public Task<IActionResult> DeleteOption(int id, int attributeId, int optionId, CancellationToken cancellationToken) =>
        RunAsync(id, async () =>
        {
            await adminApi.DeleteOptionAsync(attributeId, optionId, cancellationToken);
            return "Option deleted.";
        });

    // ---------------------------------------------------------------------

    /// <summary>400 (invalid input) and 409 (rule broken) are shown to the admin; 401/403/404 go to the exception filter.</summary>
    private static bool IsFormError(ApiException ex) => ex.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict;

    /// <summary>Runs one change and returns to the category with a flash message.</summary>
    private async Task<IActionResult> RunAsync(int categoryId, Func<Task<string>> action)
    {
        try
        {
            TempData["Success"] = await action();
        }
        catch (ApiException ex) when (IsFormError(ex))
        {
            TempData["Error"] = ex.FirstMessage;
        }

        return RedirectToAction(nameof(Index), new { id = categoryId });
    }
}
