using PCM.Infrastructure.DTO.Catalog;

namespace PCM.Infrastructure.Services;

public interface ICategoryService
{
    /// <summary>The whole category tree, ordered by SortOrder then Name at every level.</summary>
    Task<IReadOnlyList<CategoryTreeDto>> GetTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>All attributes (with options) of a category; empty for a non-leaf category. 404 if unknown.</summary>
    Task<IReadOnlyList<SpecAttributeDto>> GetAttributesAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a category. The parent must exist and hold no products or attributes,
    /// because those belong to leaf categories only (409).
    /// </summary>
    Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Renames (slug follows the name) and reorders a category.</summary>
    Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a category with its attributes; 409 if it has subcategories or products.</summary>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
