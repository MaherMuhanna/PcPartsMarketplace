using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PCM.Infrastructure.Security;
using PCM.UI.Catalog;
using PCM.UI.Models;

namespace PCM.UI.Controllers;

/// <summary>Server-rendered pieces of the shared product form, loaded with fetch when the category changes.</summary>
[Authorize(Roles = $"{Roles.Admin},{Roles.Seller}")]
[Route("product-form")]
public sealed class ProductFormController(ProductFormBuilder formBuilder) : Controller
{
    /// <summary>Empty spec inputs for a category (the category's attributes, sorted).</summary>
    [HttpGet("spec-fields")]
    public async Task<IActionResult> SpecFields(int categoryId, CancellationToken cancellationToken)
    {
        var fields = await formBuilder.GetSpecFieldsAsync(categoryId, values: null, cancellationToken);
        return PartialView("_SpecFields", new SpecFieldsModel(fields, categoryId > 0));
    }
}
