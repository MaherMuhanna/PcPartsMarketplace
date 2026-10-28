using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;
using PCM.Repositories.Common;

namespace PCM.Infrastructure.Mappings;

/// <summary>Manual entity-to-DTO mapping: explicit, debuggable, no reflection.</summary>
public static class BrandMappings
{
    public static BrandDto ToDto(this Brand brand) =>
        new(brand.Id, brand.Name, brand.Slug, brand.LogoUrl);
}

public static class PagingMappings
{
    public static PagedResponse<TDto> ToResponse<TEntity, TDto>(this PagedResult<TEntity> page, Func<TEntity, TDto> map) =>
        new(page.Items.Select(map).ToList(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
}
