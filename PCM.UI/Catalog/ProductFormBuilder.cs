using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PCM.Infrastructure.DTO.Catalog;
using PCM.UI.ApiClients;
using PCM.UI.Extensions;
using PCM.UI.Models;

namespace PCM.UI.Catalog;

/// <summary>
/// Everything the shared product form needs, used by both the admin products and the seller proposals
/// controllers: loading the lookups, turning a product into form fields, building the API request, and
/// putting the API's field errors next to the right inputs.
/// </summary>
/// <remarks>
/// Empty optional spec fields and empty image rows are not sent, so the API's list indexes can differ
/// from the form's (API "specs[1]" may be form field Specs[3]). <see cref="ToRequest"/> records the
/// mapping and <see cref="TryAddApiErrors"/> uses it.
/// </remarks>
public sealed partial class ProductFormBuilder(ICatalogApiClient catalogApi, IAdminCatalogApiClient adminCatalogApi)
{
    /// <summary>Brands are few (17 seeded); one page of the API's maximum size holds them all.</summary>
    private const int BrandPageSize = 100;

    /// <summary>Request plus, for each item of its Specs and Images lists, the index of the form row it came from.</summary>
    public sealed record BuiltRequest(ProductUpsertRequest Request, IReadOnlyList<int> SpecRows, IReadOnlyList<int> ImageRows);

    /// <summary>An empty form with one image row.</summary>
    public async Task<ProductFormViewModel> NewAsync(ProductFormMode mode, int? categoryId, CancellationToken cancellationToken)
    {
        var model = new ProductFormViewModel { Mode = mode, CategoryId = categoryId ?? 0, Images = [new ImageRowViewModel()] };
        await PrepareAsync(model, cancellationToken);
        return model;
    }

    /// <summary>The form filled with an existing product.</summary>
    public async Task<ProductFormViewModel> FromProductAsync(ProductManageDto product, ProductFormMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        var model = new ProductFormViewModel
        {
            Id = product.Id,
            Mode = mode,
            Product = product,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            Name = product.Name,
            ModelNumber = product.ModelNumber,
            Description = product.Description,
            Specs = [.. product.Specs.Select(s => new SpecFieldViewModel
            {
                AttributeId = s.AttributeId,
                TextValue = s.TextValue,
                NumberValue = s.NumberValue,
                OptionId = s.OptionId
            })],
            Images = [.. product.Images.OrderBy(i => i.SortOrder).Select(i => new ImageRowViewModel { Url = i.Url, AltText = i.AltText })]
        };

        await PrepareAsync(model, cancellationToken);
        return model;
    }

    /// <summary>
    /// Reloads the lookups (leaf categories, brands) and rebuilds the spec fields from the category's
    /// current attributes, keeping the values in the model (posted or loaded) by attribute id.
    /// </summary>
    public async Task PrepareAsync(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        var tree = await catalogApi.GetCategoriesAsync(cancellationToken);
        model.Categories = tree.Leaves();
        model.Brands = (await adminCatalogApi.GetBrandsAsync(1, BrandPageSize, "name", cancellationToken)).Items;
        model.Specs = await GetSpecFieldsAsync(model.CategoryId, model.Specs, cancellationToken);

        if (model.Images.Count == 0)
        {
            model.Images.Add(new ImageRowViewModel());
        }
    }

    /// <summary>
    /// Call first in every POST: posted spec rows carry only the attribute id and a value, so this
    /// reattaches each attribute's type (needed to pick the value to send) from the API, in render order.
    /// Values of attributes that are not in the category are dropped.
    /// </summary>
    public async Task BindSpecsAsync(ProductFormViewModel model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        model.Specs = await GetSpecFieldsAsync(model.CategoryId, model.Specs, cancellationToken);
    }

    /// <summary>Spec fields of a leaf category (empty for 0 or a non-leaf, which has no attributes).</summary>
    public async Task<List<SpecFieldViewModel>> GetSpecFieldsAsync(int categoryId, IEnumerable<SpecFieldViewModel>? values, CancellationToken cancellationToken)
    {
        if (categoryId <= 0)
        {
            return [];
        }

        try
        {
            var attributes = await adminCatalogApi.GetAttributesAsync(categoryId, cancellationToken);
            return SpecFieldViewModel.For(attributes, values);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // The category was deleted meanwhile: no fields; the API reports the category on save.
            return [];
        }
    }

    /// <summary>The API request from the posted form: trimmed texts, only filled specs and image rows.</summary>
    public static BuiltRequest ToRequest(ProductFormViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var specs = new List<SpecValueRequest>();
        var specRows = new List<int>();
        for (var row = 0; row < model.Specs.Count; row++)
        {
            var field = model.Specs[row];
            if (!field.HasValue)
            {
                continue;
            }

            // Exactly one value, the one that matches the attribute type (the API checks it again).
            specs.Add(field.DataType switch
            {
                "Text" => new SpecValueRequest { AttributeId = field.AttributeId, TextValue = field.TextValue!.Trim() },
                "Number" => new SpecValueRequest { AttributeId = field.AttributeId, NumberValue = field.NumberValue },
                _ => new SpecValueRequest { AttributeId = field.AttributeId, OptionId = field.OptionId }
            });
            specRows.Add(row);
        }

        var images = new List<ProductImageRequest>();
        var imageRows = new List<int>();
        for (var row = 0; row < model.Images.Count; row++)
        {
            var image = model.Images[row];
            if (string.IsNullOrWhiteSpace(image.Url))
            {
                continue;
            }

            images.Add(new ProductImageRequest
            {
                Url = image.Url.Trim(),
                AltText = string.IsNullOrWhiteSpace(image.AltText) ? null : image.AltText.Trim()
            });
            imageRows.Add(row);
        }

        var request = new ProductUpsertRequest
        {
            CategoryId = model.CategoryId,
            BrandId = model.BrandId,
            Name = model.Name.Trim(),
            ModelNumber = model.ModelNumber.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            Specs = specs,
            Images = images
        };

        return new BuiltRequest(request, specRows, imageRows);
    }

    /// <summary>
    /// Shows an API error on the form. 400 field errors go next to their inputs, with list indexes
    /// translated back to form rows ("specs[1].optionId" -> "Specs[3].optionId"; ModelState keys
    /// ignore case). Returns false for 401/403/404, which the caller rethrows to the exception filter.
    /// </summary>
    public static bool TryAddApiErrors(ModelStateDictionary modelState, ApiException exception, BuiltRequest built)
    {
        ArgumentNullException.ThrowIfNull(modelState);
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(built);

        if (exception.StatusCode != HttpStatusCode.BadRequest || exception.Problem?.Errors is not { Count: > 0 } errors)
        {
            return modelState.TryAddApiError(exception);
        }

        foreach (var (field, messages) in errors)
        {
            var key = MapKey(field, built);
            foreach (var message in messages)
            {
                modelState.AddModelError(key, message);
            }
        }

        return true;
    }

    private static string MapKey(string field, BuiltRequest built)
    {
        var match = ListItemKey().Match(field);
        if (match.Success)
        {
            var rows = match.Groups["list"].Value.Equals("specs", StringComparison.OrdinalIgnoreCase) ? built.SpecRows : built.ImageRows;
            var index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
            var row = index < rows.Count ? rows[index] : index;
            var list = match.Groups["list"].Value.Equals("specs", StringComparison.OrdinalIgnoreCase) ? "Specs" : "Images";
            return $"{list}[{row}]{match.Groups["rest"].Value}";
        }

        return field.Length == 0 ? string.Empty : char.ToUpperInvariant(field[0]) + field[1..];
    }

    [GeneratedRegex(@"^(?<list>specs|images)\[(?<index>\d+)\](?<rest>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemKey();
}
