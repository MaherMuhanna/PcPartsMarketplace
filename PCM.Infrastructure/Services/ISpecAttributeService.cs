using PCM.Infrastructure.DTO.Catalog;

namespace PCM.Infrastructure.Services;

/// <summary>Admin management of the spec attributes of leaf categories and of their Enum options.</summary>
public interface ISpecAttributeService
{
    /// <summary>Adds an attribute to a leaf category; 409 if the category has subcategories or the name/slug is taken.</summary>
    Task<SpecAttributeDto> CreateAsync(int categoryId, SpecAttributeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an attribute. Changing the data type is allowed only while no product uses it (409).</summary>
    Task<SpecAttributeDto> UpdateAsync(int categoryId, int attributeId, SpecAttributeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes an attribute and its options; 409 while any product has a value for it.</summary>
    Task DeleteAsync(int categoryId, int attributeId, CancellationToken cancellationToken = default);

    /// <summary>Adds an option to an Enum attribute (409 for other types or a duplicate value).</summary>
    Task<SpecOptionDto> CreateOptionAsync(int attributeId, SpecOptionRequest request, CancellationToken cancellationToken = default);

    Task<SpecOptionDto> UpdateOptionAsync(int attributeId, int optionId, SpecOptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes an option; 409 while any product uses it.</summary>
    Task DeleteOptionAsync(int attributeId, int optionId, CancellationToken cancellationToken = default);
}
