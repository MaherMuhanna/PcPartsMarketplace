using FluentValidation;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Catalog;

namespace PCM.Infrastructure.Validators;

// Shape rules only (lengths, ranges, formats). Rules that need the database
// (leaf category, option belongs to attribute, unique names) live in the services.

public sealed class ProductListQueryValidator : PageQueryValidator<ProductListQuery>
{
    public ProductListQueryValidator() : base(ProductListQuery.AllowedSorts)
    {
        RuleFor(q => q.Q).MaximumLength(100);
        RuleFor(q => q.Category).MaximumLength(100);
        RuleForEach(q => q.Brand).MaximumLength(500);
        RuleFor(q => q.Brand)
            .Must(brands => brands is null || brands.Length <= 20)
            .WithMessage("At most 20 brand values.");

        RuleFor(q => q.MinPrice).GreaterThanOrEqualTo(0m);
        RuleFor(q => q.MaxPrice).GreaterThanOrEqualTo(0m);
        RuleFor(q => q.MaxPrice)
            .Must((q, max) => max >= q.MinPrice)
            .When(q => q.MinPrice is not null && q.MaxPrice is not null)
            .WithMessage("Max price must be greater than or equal to min price.");
    }
}

public sealed class AdminProductListQueryValidator : PageQueryValidator<AdminProductListQuery>
{
    public AdminProductListQueryValidator() : base(AdminProductListQuery.AllowedSorts)
    {
        RuleFor(q => q.Status).ValidProductStatus();
        RuleFor(q => q.CategoryId).GreaterThan(0);
        RuleFor(q => q.Q).MaximumLength(100);
    }
}

public sealed class SellerProposalListQueryValidator : PageQueryValidator<SellerProposalListQuery>
{
    public SellerProposalListQueryValidator() : base(SellerProposalListQuery.AllowedSorts)
    {
        RuleFor(q => q.Status).ValidProductStatus();
    }
}

public sealed partial class ProductUpsertRequestValidator : AbstractValidator<ProductUpsertRequest>
{
    public const int MaxImages = 8;

    public ProductUpsertRequestValidator()
    {
        RuleFor(r => r.CategoryId).GreaterThan(0);
        RuleFor(r => r.BrandId).GreaterThan(0);
        RuleFor(r => r.Name).TrimmedLength(3, 200);

        // ModelNumber is varchar (ASCII) in the database.
        RuleFor(r => r.ModelNumber)
            .TrimmedLength(1, 100)
            .Matches(@"^[\x20-\x7E]+$").WithMessage("Model number may only contain printable ASCII characters.");

        RuleFor(r => r.Description).MaximumLength(4000);

        RuleFor(r => r.Specs)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(specs => specs.Select(s => s.AttributeId).Distinct().Count() == specs.Count)
            .WithMessage("Each attribute can appear only once.");

        RuleForEach(r => r.Specs).ChildRules(spec =>
        {
            spec.RuleFor(s => s.AttributeId).GreaterThan(0);

            spec.RuleFor(s => s)
                .Must(s => (s.TextValue is not null ? 1 : 0) + (s.NumberValue is not null ? 1 : 0) + (s.OptionId is not null ? 1 : 0) == 1)
                .OverridePropertyName("value")
                .WithMessage("Set exactly one of textValue, numberValue or optionId.");

            spec.RuleFor(s => s.TextValue).TrimmedLength(1, 200).When(s => s.TextValue is not null);
            spec.RuleFor(s => s.NumberValue).InclusiveBetween(0m, 1_000_000_000m);
            spec.RuleFor(s => s.OptionId).GreaterThan(0);
        });

        RuleFor(r => r.Images)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(images => images.Count <= MaxImages)
            .WithMessage($"A product can have at most {MaxImages} images.");

        RuleForEach(r => r.Images).ChildRules(image =>
        {
            image.RuleFor(i => i.Url)
                .NotEmpty()
                .MaximumLength(500)
                .Must(BeImageUrl)
                .WithMessage("Url must be an absolute http(s) URL or a site path starting with '/'.");

            image.RuleFor(i => i.AltText).MaximumLength(200);
        });
    }

    /// <summary>
    /// http(s) URL, or a path on this site such as /images/products/cpu.svg. A path must start with
    /// "/" followed by a letter or digit: "//host" and "/\host" are read by browsers as another site.
    /// </summary>
    private static bool BeImageUrl(string url) =>
        url.StartsWith('/')
            ? SitePath().IsMatch(url)
            : Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    [System.Text.RegularExpressions.GeneratedRegex(@"^/[A-Za-z0-9][A-Za-z0-9/._-]*$")]
    private static partial System.Text.RegularExpressions.Regex SitePath();
}

public sealed class ProductReviewRequestValidator : AbstractValidator<ProductReviewRequest>
{
    public ProductReviewRequestValidator()
    {
        RuleFor(r => r.Note).TrimmedLength(3, 500);
    }
}

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(r => r.ParentId).GreaterThan(0);
        RuleFor(r => r.Name).TrimmedLength(2, 100);
        RuleFor(r => r.SortOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(r => r.Name).TrimmedLength(2, 100);
        RuleFor(r => r.SortOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class SpecAttributeRequestValidator : AbstractValidator<SpecAttributeRequest>
{
    public SpecAttributeRequestValidator()
    {
        RuleFor(r => r.Name).TrimmedLength(1, 100);

        RuleFor(r => r.DataType)
            .Must(type => Enum.TryParse<SpecDataType>(type, ignoreCase: true, out _))
            .WithMessage($"Data type must be one of: {string.Join(", ", Enum.GetNames<SpecDataType>())}.");

        RuleFor(r => r.Unit).MaximumLength(20);

        // A free-text value cannot be offered as a checkbox or a range.
        RuleFor(r => r.IsFilterable)
            .Equal(false)
            .When(r => string.Equals(r.DataType, nameof(SpecDataType.Text), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Text attributes cannot be filterable.");

        RuleFor(r => r.SortOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class SpecOptionRequestValidator : AbstractValidator<SpecOptionRequest>
{
    public SpecOptionRequestValidator()
    {
        RuleFor(r => r.Value).TrimmedLength(1, 100);
        RuleFor(r => r.SortOrder).InclusiveBetween(0, 10_000);
    }
}

public sealed class BrandRequestValidator : AbstractValidator<BrandRequest>
{
    public BrandRequestValidator()
    {
        RuleFor(r => r.Name).TrimmedLength(2, 100);

        RuleFor(r => r.LogoUrl)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            .When(r => !string.IsNullOrWhiteSpace(r.LogoUrl))
            .WithMessage("Logo URL must be an absolute http(s) URL.");
    }
}

internal static class CatalogRules
{
    /// <summary>Required text whose trimmed length is within the range.</summary>
    public static IRuleBuilderOptions<T, string?> TrimmedLength<T>(this IRuleBuilder<T, string?> rule, int min, int max) =>
        rule.NotEmpty()
            .Must(value => value!.Trim().Length >= min && value.Trim().Length <= max)
            .WithMessage($"'{{PropertyName}}' must be between {min} and {max} characters.");

    /// <summary>Optional product status filter.</summary>
    public static IRuleBuilderOptions<T, string?> ValidProductStatus<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(status => status is null || Enum.TryParse<ProductStatus>(status, ignoreCase: true, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<ProductStatus>())}.");
}
