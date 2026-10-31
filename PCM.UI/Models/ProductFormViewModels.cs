using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Validators;

namespace PCM.UI.Models;

/// <summary>Who uses the shared product form: an admin (any product) or a seller (own proposals).</summary>
public enum ProductFormMode
{
    Admin,
    Seller
}

/// <summary>
/// The shared create/edit product form (admin products and seller proposals, partial _ProductForm).
/// Posted fields: ids, texts, Specs[i] and Images[i]. Lookups and the current product are reloaded
/// from the API on every render, never trusted from the post.
/// </summary>
public sealed class ProductFormViewModel
{
    /// <summary>Null when creating.</summary>
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Choose a brand.")]
    [Display(Name = "Brand")]
    public int BrandId { get; set; }

    [Required, StringLength(200, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Model number")]
    public string ModelNumber { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    /// <summary>One field per attribute of the category, in the order they are rendered (and posted).</summary>
    public List<SpecFieldViewModel> Specs { get; set; } = [];

    /// <summary>Image rows; empty rows are ignored. The first image is the main one.</summary>
    public List<ImageRowViewModel> Images { get; set; } = [];

    // ---- Display only ----

    [BindNever, ValidateNever]
    public ProductFormMode Mode { get; set; }

    /// <summary>The product being edited (status, review note, proposer); null when creating.</summary>
    [BindNever, ValidateNever]
    public ProductManageDto? Product { get; set; }

    /// <summary>Leaf categories, the only ones a product can belong to.</summary>
    [BindNever, ValidateNever]
    public IReadOnlyList<CategoryEntry> Categories { get; set; } = [];

    [BindNever, ValidateNever]
    public IReadOnlyList<BrandDto> Brands { get; set; } = [];

    /// <summary>True when the product already exists (category changes then wipe the specs and need a confirm).</summary>
    public bool IsEdit => Id is not null;
}

/// <summary>
/// One spec field. Only the value that matches <see cref="DataType"/> is used: TextValue (Text),
/// NumberValue (Number) or OptionId (Enum). The attribute's details are reloaded from the API.
/// </summary>
public sealed class SpecFieldViewModel
{
    public int AttributeId { get; set; }

    [StringLength(200)]
    public string? TextValue { get; set; }

    public decimal? NumberValue { get; set; }

    public int? OptionId { get; set; }

    // ---- Display only ----

    [BindNever, ValidateNever]
    public string Name { get; set; } = string.Empty;

    [BindNever, ValidateNever]
    public string DataType { get; set; } = string.Empty;

    [BindNever, ValidateNever]
    public string? Unit { get; set; }

    [BindNever, ValidateNever]
    public bool IsRequired { get; set; }

    [BindNever, ValidateNever]
    public IReadOnlyList<SpecOptionDto> Options { get; set; } = [];

    /// <summary>True when the field holds a value (empty optional fields are not sent to the API).</summary>
    public bool HasValue => DataType switch
    {
        "Text" => !string.IsNullOrWhiteSpace(TextValue),
        "Number" => NumberValue is not null,
        "Enum" => OptionId is not null,
        _ => false
    };

    /// <summary>
    /// Fields for every attribute of a category (sorted as the admin set them), filled with the
    /// values in <paramref name="values"/> matched by attribute id.
    /// </summary>
    public static List<SpecFieldViewModel> For(IEnumerable<SpecAttributeDto> attributes, IEnumerable<SpecFieldViewModel>? values)
    {
        var byAttribute = (values ?? []).GroupBy(v => v.AttributeId).ToDictionary(g => g.Key, g => g.First());

        return [.. attributes
            .OrderBy(a => a.SortOrder).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(a =>
            {
                byAttribute.TryGetValue(a.Id, out var value);
                return new SpecFieldViewModel
                {
                    AttributeId = a.Id,
                    Name = a.Name,
                    DataType = a.DataType,
                    Unit = a.Unit,
                    IsRequired = a.IsRequired,
                    Options = [.. a.Options.OrderBy(o => o.SortOrder).ThenBy(o => o.Value, StringComparer.OrdinalIgnoreCase)],
                    TextValue = value?.TextValue,
                    NumberValue = value?.NumberValue,
                    // An option id that no longer belongs to the attribute is dropped.
                    OptionId = value?.OptionId is { } id && a.Options.Any(o => o.Id == id) ? id : null
                };
            })];
    }
}

/// <summary>One image row of the product form.</summary>
public sealed partial class ImageRowViewModel
{
    [StringLength(500)]
    public string? Url { get; set; }

    [StringLength(200), Display(Name = "Alt text")]
    public string? AltText { get; set; }

    /// <summary>
    /// Same rule as the API (ProductUpsertRequestValidator): http(s) URL or a site path starting with
    /// "/" and a letter or digit. Used to decide whether the preview may load the URL.
    /// </summary>
    public bool IsPreviewable =>
        Url is { Length: > 0 } url
        && (url.StartsWith('/')
            ? SitePath().IsMatch(url)
            : Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp));

    /// <summary>Most images a product can have (shared with the API).</summary>
    public const int MaxImages = ProductUpsertRequestValidator.MaxImages;

    [GeneratedRegex(@"^/[A-Za-z0-9][A-Za-z0-9/._-]*$")]
    private static partial Regex SitePath();
}

/// <summary>Input of the _SpecFields partial (also returned alone when the category changes).</summary>
/// <param name="Fields">Fields in render order; their index is the Specs[i] index.</param>
/// <param name="CategoryChosen">False before a category is picked (shows a hint instead of fields).</param>
public sealed record SpecFieldsModel(IReadOnlyList<SpecFieldViewModel> Fields, bool CategoryChosen);
