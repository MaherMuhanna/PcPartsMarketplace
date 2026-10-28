using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.Catalog;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;

namespace PCM.Infrastructure.Services;

public sealed class CategoryService(IUnitOfWork unitOfWork) : ICategoryService
{
    private const int SlugMaxLength = 100;

    public async Task<IReadOnlyList<CategoryTreeDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var tree = new CategoryTree(await unitOfWork.Categories.GetAllAsync(cancellationToken));
        return tree.Roots.Select(root => ToTreeDto(tree, root)).ToList();
    }

    public async Task<IReadOnlyList<SpecAttributeDto>> GetAttributesAsync(int categoryId, CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Categories.ExistsAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new NotFoundException($"Category {categoryId} was not found.");
        }

        var attributes = await unitOfWork.SpecAttributes.GetByCategoryAsync(categoryId, cancellationToken);
        return attributes.Select(a => a.ToDto()).ToList();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (request.ParentId is { } parentId)
        {
            if (!await unitOfWork.Categories.ExistsAsync(c => c.Id == parentId, cancellationToken))
            {
                throw new RequestValidationException("parentId", $"Category {parentId} was not found.");
            }

            // Products and attributes live on leaves; a parent that has them must stay a leaf.
            if (await unitOfWork.Products.ExistsAsync(p => p.CategoryId == parentId, cancellationToken)
                || await unitOfWork.SpecAttributes.ExistsAsync(a => a.CategoryId == parentId, cancellationToken))
            {
                throw new ConflictException("The parent category has products or attributes, so it cannot get subcategories.");
            }
        }

        await EnsureNameIsFreeAsync(request.ParentId, name, excludeId: 0, cancellationToken);

        var category = new Category
        {
            ParentId = request.ParentId,
            Name = name,
            Slug = await UniqueSlugAsync(name, excludeId: 0, cancellationToken),
            SortOrder = request.SortOrder
        };

        await unitOfWork.Categories.AddAsync(category, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return category.ToDto();
    }

    public async Task<CategoryDto> UpdateAsync(int id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await unitOfWork.Categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category {id} was not found.");

        var name = request.Name.Trim();
        if (!string.Equals(category.Name, name, StringComparison.Ordinal))
        {
            await EnsureNameIsFreeAsync(category.ParentId, name, excludeId: id, cancellationToken);
            category.Name = name;
            category.Slug = await UniqueSlugAsync(name, excludeId: id, cancellationToken);
        }

        category.SortOrder = request.SortOrder;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return category.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await unitOfWork.Categories.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Category {id} was not found.");

        if (await unitOfWork.Categories.ExistsAsync(c => c.ParentId == id, cancellationToken))
        {
            throw new ConflictException($"Category '{category.Name}' has subcategories. Delete them first.");
        }

        if (await unitOfWork.Products.ExistsAsync(p => p.CategoryId == id, cancellationToken))
        {
            throw new ConflictException($"Category '{category.Name}' has products and cannot be deleted.");
        }

        // No products means no spec values, so the attributes and options can go with it.
        foreach (var listed in await unitOfWork.SpecAttributes.GetByCategoryAsync(id, cancellationToken))
        {
            var attribute = await unitOfWork.SpecAttributes.GetWithOptionsAsync(listed.Id, cancellationToken);
            if (attribute is null)
            {
                continue;
            }

            foreach (var option in attribute.SpecOptions)
            {
                unitOfWork.SpecOptions.Remove(option);
            }

            unitOfWork.SpecAttributes.Remove(attribute);
        }

        unitOfWork.Categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------

    private static CategoryTreeDto ToTreeDto(CategoryTree tree, Category category)
    {
        var children = tree.ChildrenOf(category.Id).Select(child => ToTreeDto(tree, child)).ToList();
        return new CategoryTreeDto(category.Id, category.Name, category.Slug, category.SortOrder, children.Count == 0, children);
    }

    /// <summary>Sibling names are unique (UX_Category_ParentId_Name).</summary>
    private async Task EnsureNameIsFreeAsync(int? parentId, string name, int excludeId, CancellationToken cancellationToken)
    {
        if (await unitOfWork.Categories.ExistsAsync(c => c.ParentId == parentId && c.Name == name && c.Id != excludeId, cancellationToken))
        {
            throw new ConflictException($"A category named '{name}' already exists at this level.");
        }
    }

    /// <summary>Slug from the name; "-2", "-3"... when another category already uses it.</summary>
    private async Task<string> UniqueSlugAsync(string name, int excludeId, CancellationToken cancellationToken)
    {
        var baseSlug = SlugGenerator.Generate(name, SlugMaxLength);
        for (var attempt = 1; ; attempt++)
        {
            var candidate = SlugGenerator.WithSuffix(baseSlug, attempt, SlugMaxLength);
            if (!await unitOfWork.Categories.ExistsAsync(c => c.Slug == candidate && c.Id != excludeId, cancellationToken))
            {
                return candidate;
            }
        }
    }
}
