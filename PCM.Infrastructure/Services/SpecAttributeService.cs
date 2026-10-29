using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Catalog;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;

namespace PCM.Infrastructure.Services;

public sealed class SpecAttributeService(IUnitOfWork unitOfWork) : ISpecAttributeService
{
    private const int SlugMaxLength = 100;

    public async Task<SpecAttributeDto> CreateAsync(int categoryId, SpecAttributeRequest request, CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Categories.ExistsAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new NotFoundException($"Category {categoryId} was not found.");
        }

        if (await unitOfWork.Categories.ExistsAsync(c => c.ParentId == categoryId, cancellationToken))
        {
            throw new ConflictException("Attributes can be added to leaf categories only (this category has subcategories).");
        }

        var attribute = new SpecAttribute { CategoryId = categoryId };
        await ApplyAsync(attribute, request, cancellationToken);

        await unitOfWork.SpecAttributes.AddAsync(attribute, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return attribute.ToDto();
    }

    public async Task<SpecAttributeDto> UpdateAsync(int categoryId, int attributeId, SpecAttributeRequest request, CancellationToken cancellationToken = default)
    {
        var attribute = await GetAttributeAsync(categoryId, attributeId, cancellationToken);
        var newType = ParseType(request.DataType);

        if (!attribute.IsDataType(newType))
        {
            // Stored values are typed (TextValue / NumberValue / SpecOptionId), so they would no longer match.
            if (await unitOfWork.SpecAttributes.IsInUseAsync(attribute.Id, cancellationToken))
            {
                throw new ConflictException($"Attribute '{attribute.Name}' is used by products, so its data type cannot change.");
            }

            // Leaving Enum: its options no longer mean anything.
            if (attribute.IsDataType(SpecDataType.Enum))
            {
                foreach (var option in attribute.SpecOptions)
                {
                    unitOfWork.SpecOptions.Remove(option);
                }
            }
        }

        await ApplyAsync(attribute, request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return attribute.ToDto();
    }

    public async Task DeleteAsync(int categoryId, int attributeId, CancellationToken cancellationToken = default)
    {
        var attribute = await GetAttributeAsync(categoryId, attributeId, cancellationToken);

        if (await unitOfWork.SpecAttributes.IsInUseAsync(attribute.Id, cancellationToken))
        {
            throw new ConflictException($"Attribute '{attribute.Name}' is used by products and cannot be deleted.");
        }

        foreach (var option in attribute.SpecOptions)
        {
            unitOfWork.SpecOptions.Remove(option);
        }

        unitOfWork.SpecAttributes.Remove(attribute);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SpecOptionDto> CreateOptionAsync(int attributeId, SpecOptionRequest request, CancellationToken cancellationToken = default)
    {
        var attribute = await GetEnumAttributeAsync(attributeId, cancellationToken);

        var option = new SpecOption { SpecAttributeId = attribute.Id };
        await ApplyOptionAsync(option, request, cancellationToken);

        await unitOfWork.SpecOptions.AddAsync(option, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return option.ToDto();
    }

    public async Task<SpecOptionDto> UpdateOptionAsync(int attributeId, int optionId, SpecOptionRequest request, CancellationToken cancellationToken = default)
    {
        var attribute = await GetEnumAttributeAsync(attributeId, cancellationToken);
        var option = attribute.SpecOptions.FirstOrDefault(o => o.Id == optionId)
            ?? throw new NotFoundException($"Option {optionId} was not found on attribute {attributeId}.");

        await ApplyOptionAsync(option, request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return option.ToDto();
    }

    public async Task DeleteOptionAsync(int attributeId, int optionId, CancellationToken cancellationToken = default)
    {
        var attribute = await GetEnumAttributeAsync(attributeId, cancellationToken);
        var option = attribute.SpecOptions.FirstOrDefault(o => o.Id == optionId)
            ?? throw new NotFoundException($"Option {optionId} was not found on attribute {attributeId}.");

        if (await unitOfWork.SpecAttributes.IsOptionInUseAsync(option.Id, cancellationToken))
        {
            throw new ConflictException($"Option '{option.Value}' is used by products and cannot be deleted.");
        }

        unitOfWork.SpecOptions.Remove(option);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------

    private async Task<SpecAttribute> GetAttributeAsync(int categoryId, int attributeId, CancellationToken cancellationToken)
    {
        var attribute = await unitOfWork.SpecAttributes.GetWithOptionsAsync(attributeId, cancellationToken);
        return attribute is not null && attribute.CategoryId == categoryId
            ? attribute
            : throw new NotFoundException($"Attribute {attributeId} was not found in category {categoryId}.");
    }

    private async Task<SpecAttribute> GetEnumAttributeAsync(int attributeId, CancellationToken cancellationToken)
    {
        var attribute = await unitOfWork.SpecAttributes.GetWithOptionsAsync(attributeId, cancellationToken)
            ?? throw new NotFoundException($"Attribute {attributeId} was not found.");

        return attribute.IsDataType(SpecDataType.Enum)
            ? attribute
            : throw new ConflictException($"Attribute '{attribute.Name}' is {attribute.DataType}; only Enum attributes have options.");
    }

    /// <summary>Copies the request onto the attribute after checking name and slug inside the category.</summary>
    private async Task ApplyAsync(SpecAttribute attribute, SpecAttributeRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var slug = SlugGenerator.Generate(name, SlugMaxLength);

        // The slug becomes a query-string key of GET /products, so it must not shadow a built-in one.
        if (CatalogQueryKeys.IsReserved(slug))
        {
            throw new ConflictException($"The name '{name}' gives the slug '{slug}', which is reserved by the catalog search. Choose another name.");
        }

        if (await unitOfWork.SpecAttributes.ExistsAsync(
                a => a.CategoryId == attribute.CategoryId && a.Id != attribute.Id && (a.Name == name || a.Slug == slug),
                cancellationToken))
        {
            throw new ConflictException($"This category already has an attribute named '{name}' (or with the slug '{slug}').");
        }

        attribute.Name = name;
        attribute.Slug = slug;
        attribute.DataType = ParseType(request.DataType).ToString();
        attribute.Unit = string.IsNullOrWhiteSpace(request.Unit) ? null : request.Unit.Trim();
        attribute.IsFilterable = request.IsFilterable;
        attribute.IsRequired = request.IsRequired;
        attribute.SortOrder = request.SortOrder;
    }

    private async Task ApplyOptionAsync(SpecOption option, SpecOptionRequest request, CancellationToken cancellationToken)
    {
        var value = request.Value.Trim();
        var slug = SlugGenerator.Generate(value, SlugMaxLength);

        if (await unitOfWork.SpecOptions.ExistsAsync(
                o => o.SpecAttributeId == option.SpecAttributeId && o.Id != option.Id && (o.Value == value || o.Slug == slug),
                cancellationToken))
        {
            throw new ConflictException($"This attribute already has the option '{value}' (or one with the slug '{slug}').");
        }

        option.Value = value;
        option.Slug = slug;
        option.SortOrder = request.SortOrder;
    }

    /// <summary>Canonical casing ("number" -> Number). The validator has already checked the value.</summary>
    private static SpecDataType ParseType(string dataType) => Enum.Parse<SpecDataType>(dataType, ignoreCase: true);
}
