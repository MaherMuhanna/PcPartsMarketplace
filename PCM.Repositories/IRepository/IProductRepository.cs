using PCM.DomainEntities.DBEntities;
using PCM.Repositories.Common;
using PCM.Repositories.Models;

namespace PCM.Repositories.IRepository;

/// <summary>
/// Products. "Published" means visible in the public catalog:
/// Status = Approved, IsActive = 1 and not soft-deleted.
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    // ---- Public catalog (read-only) ----

    /// <summary>Searches published products: filters, sort and paging all run in SQL.</summary>
    Task<PagedResult<ProductListRow>> SearchAsync(
        ProductSearchCriteria criteria, int page, int pageSize, ProductSort sort, CancellationToken cancellationToken = default);

    /// <summary>Published product with brand, category, images and spec values, or null.</summary>
    Task<Product?> GetPublishedByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Published product by slug (see <see cref="GetPublishedByIdAsync"/>), or null.</summary>
    Task<Product?> GetPublishedBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Offers shown on the product page: approved sellers only, cheapest first.</summary>
    Task<IReadOnlyList<ProductOfferRow>> GetVisibleOffersAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>True when the product is published (sellers can only offer published products).</summary>
    Task<bool> IsPublishedAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Slug to id of the published products among <paramref name="slugs"/> (unknown slugs are left out).</summary>
    Task<IReadOnlyDictionary<string, int>> GetPublishedIdsBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default);

    /// <summary>Published products per brand inside the given categories (null = all).</summary>
    Task<IReadOnlyList<FacetCount>> GetBrandFacetsAsync(IReadOnlyCollection<int>? categoryIds, CancellationToken cancellationToken = default);

    /// <summary>Published products per Enum option of a leaf category's attributes.</summary>
    Task<IReadOnlyList<FacetCount>> GetOptionFacetsAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>Distinct values of every Number attribute of a leaf category with product counts, over published products.</summary>
    Task<IReadOnlyList<NumberFacet>> GetNumberFacetsAsync(int categoryId, CancellationToken cancellationToken = default);

    // ---- Admin and seller management (any status) ----

    /// <summary>
    /// Tracked product with everything needed to edit it (brand, category, proposing seller,
    /// images, spec values with their attribute and option), or null.
    /// </summary>
    Task<Product?> GetForEditAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Read-only page of products in any status, with brand, category and proposing seller.</summary>
    Task<PagedResult<Product>> GetManagePagedAsync(
        ProductManageFilter filter, int page, int pageSize, ProductManageSort sort, CancellationToken cancellationToken = default);
}
