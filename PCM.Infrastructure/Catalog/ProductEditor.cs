using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;

namespace PCM.Infrastructure.Catalog;

/// <summary>
/// Shared write logic for products, used by both the admin and the seller-proposal services:
/// checks the rules that need the database, then copies the request onto a tracked product
/// (fields, slug, spec values, images). It never saves; the calling service does.
/// </summary>
public sealed class ProductEditor(IUnitOfWork unitOfWork)
{
    public const int SlugMaxLength = 250;

    /// <summary>
    /// Applies <paramref name="request"/> to <paramref name="product"/>. For an existing product,
    /// load it with IProductRepository.GetForEditAsync so its spec values and images are tracked.
    /// </summary>
    /// <exception cref="RequestValidationException">Category is not a leaf, brand unknown, or a spec value breaks the category's attributes.</exception>
    /// <exception cref="ConflictException">Another product of the brand already has the model number.</exception>
    public async Task ApplyAsync(Product product, ProductUpsertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(request);

        await EnsureLeafCategoryAsync(request.CategoryId, cancellationToken);

        if (!await unitOfWork.Brands.ExistsAsync(b => b.Id == request.BrandId, cancellationToken))
        {
            throw new RequestValidationException("brandId", $"Brand {request.BrandId} was not found.");
        }

        var name = request.Name.Trim();
        var modelNumber = request.ModelNumber.Trim();

        // Same physical part listed twice (UX_Product_BrandId_ModelNumber).
        if (await unitOfWork.Products.ExistsAsync(
                p => p.Id != product.Id && p.BrandId == request.BrandId && p.ModelNumber == modelNumber, cancellationToken))
        {
            throw new ConflictException($"A product with model number '{modelNumber}' already exists for this brand.");
        }

        var attributes = await unitOfWork.SpecAttributes.GetByCategoryAsync(request.CategoryId, cancellationToken);
        ValidateSpecs(request.Specs, attributes);

        if (product.Id == 0 || !string.Equals(product.Name, name, StringComparison.Ordinal))
        {
            product.Slug = await UniqueSlugAsync(name, product.Id, cancellationToken);
        }

        product.CategoryId = request.CategoryId;
        product.BrandId = request.BrandId;
        product.Name = name;
        product.ModelNumber = modelNumber;
        product.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        SyncSpecValues(product, request.Specs);
        SyncImages(product, request.Images);
    }

    // ---------------------------------------------------------------------

    private async Task EnsureLeafCategoryAsync(int categoryId, CancellationToken cancellationToken)
    {
        if (!await unitOfWork.Categories.ExistsAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new RequestValidationException("categoryId", $"Category {categoryId} was not found.");
        }

        if (await unitOfWork.Categories.ExistsAsync(c => c.ParentId == categoryId, cancellationToken))
        {
            throw new RequestValidationException("categoryId", "Products can only be added to a leaf category (one without subcategories).");
        }
    }

    /// <summary>
    /// Every value must belong to an attribute of the category and match its type;
    /// Enum options must belong to their attribute; every required attribute must have a value.
    /// All problems are reported together, keyed like the request ("specs[2].optionId").
    /// </summary>
    public static void ValidateSpecs(IReadOnlyList<SpecValueRequest> specs, IReadOnlyList<SpecAttribute> attributes)
    {
        var errors = new Dictionary<string, string[]>();
        var byId = attributes.ToDictionary(a => a.Id);

        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i];
            var key = $"specs[{i}]";

            if (!byId.TryGetValue(spec.AttributeId, out var attribute))
            {
                errors[$"{key}.attributeId"] = [$"Attribute {spec.AttributeId} does not belong to the selected category."];
                continue;
            }

            switch (Enum.Parse<SpecDataType>(attribute.DataType))
            {
                case SpecDataType.Text when spec.TextValue is null:
                    errors[$"{key}.textValue"] = [$"'{attribute.Name}' is a text attribute: set textValue."];
                    break;
                case SpecDataType.Number when spec.NumberValue is null:
                    errors[$"{key}.numberValue"] = [$"'{attribute.Name}' is a number attribute: set numberValue."];
                    break;
                case SpecDataType.Enum when spec.OptionId is null:
                    errors[$"{key}.optionId"] = [$"'{attribute.Name}' is a list attribute: set optionId."];
                    break;
                case SpecDataType.Enum when attribute.SpecOptions.All(o => o.Id != spec.OptionId):
                    errors[$"{key}.optionId"] = [$"Option {spec.OptionId} is not a value of '{attribute.Name}'."];
                    break;
            }
        }

        var provided = specs.Select(s => s.AttributeId).ToHashSet();
        var missing = attributes.Where(a => a.IsRequired && !provided.Contains(a.Id)).Select(a => a.Name).ToList();
        if (missing.Count > 0)
        {
            errors["specs"] = [$"Required attributes are missing: {string.Join(", ", missing)}."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    /// <summary>
    /// Updates values in place, adds new ones and removes the ones no longer sent.
    /// (In place, because the key is (ProductId, SpecAttributeId): re-adding a removed key would clash.)
    /// </summary>
    private static void SyncSpecValues(Product product, IReadOnlyList<SpecValueRequest> specs)
    {
        var requested = specs.ToDictionary(s => s.AttributeId);

        foreach (var existing in product.ProductSpecValues.Where(v => !requested.ContainsKey(v.SpecAttributeId)).ToList())
        {
            product.ProductSpecValues.Remove(existing);   // required relationship: EF deletes the orphan
        }

        foreach (var spec in specs)
        {
            var value = product.ProductSpecValues.FirstOrDefault(v => v.SpecAttributeId == spec.AttributeId);
            if (value is null)
            {
                value = new ProductSpecValue { SpecAttributeId = spec.AttributeId };
                product.ProductSpecValues.Add(value);
            }

            // Exactly one is set (validator + ValidateSpecs); the others are cleared.
            value.TextValue = spec.TextValue?.Trim();
            value.NumberValue = spec.NumberValue;
            value.SpecOptionId = spec.OptionId;
        }
    }

    /// <summary>Replaces the images; list order becomes SortOrder (0 = main image).</summary>
    private static void SyncImages(Product product, IReadOnlyList<ProductImageRequest> images)
    {
        product.ProductImages.Clear();
        for (var i = 0; i < images.Count; i++)
        {
            product.ProductImages.Add(new ProductImage
            {
                Url = images[i].Url.Trim(),
                AltText = string.IsNullOrWhiteSpace(images[i].AltText) ? null : images[i].AltText!.Trim(),
                SortOrder = i
            });
        }
    }

    private async Task<string> UniqueSlugAsync(string name, int excludeId, CancellationToken cancellationToken)
    {
        var baseSlug = SlugGenerator.Generate(name, SlugMaxLength);
        for (var attempt = 1; ; attempt++)
        {
            var candidate = SlugGenerator.WithSuffix(baseSlug, attempt, SlugMaxLength);
            if (!await unitOfWork.Products.ExistsAsync(p => p.Slug == candidate && p.Id != excludeId, cancellationToken))
            {
                return candidate;
            }
        }
    }
}
