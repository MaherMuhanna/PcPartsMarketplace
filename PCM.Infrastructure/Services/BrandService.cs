using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.Catalog;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;
using PCM.Repositories.IRepository;

namespace PCM.Infrastructure.Services;

public sealed class BrandService(IUnitOfWork unitOfWork) : IBrandService
{
    private const int SlugMaxLength = 100;

    public async Task<PagedResponse<BrandDto>> GetBrandsAsync(BrandListQuery query, CancellationToken cancellationToken = default)
    {
        var page = await unitOfWork.Brands.GetPagedAsync(query.Page, query.PageSize, ParseSort(query.Sort), cancellationToken);
        return page.ToResponse(brand => brand.ToDto());
    }

    public async Task<BrandDto> CreateAsync(BrandRequest request, CancellationToken cancellationToken = default)
    {
        var brand = new Brand();
        await ApplyAsync(brand, request, cancellationToken);

        await unitOfWork.Brands.AddAsync(brand, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return brand.ToDto();
    }

    public async Task<BrandDto> UpdateAsync(int id, BrandRequest request, CancellationToken cancellationToken = default)
    {
        var brand = await unitOfWork.Brands.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Brand {id} was not found.");

        await ApplyAsync(brand, request, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return brand.ToDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var brand = await unitOfWork.Brands.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Brand {id} was not found.");

        if (await unitOfWork.Products.ExistsAsync(p => p.BrandId == id, cancellationToken))
        {
            throw new ConflictException($"Brand '{brand.Name}' has products and cannot be deleted.");
        }

        unitOfWork.Brands.Remove(brand);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Maps the public sort key to the repository sort order (default: by name).</summary>
    internal static BrandSort ParseSort(string? sort) => sort?.ToLowerInvariant() switch
    {
        "-name" => BrandSort.NameDesc,
        "newest" => BrandSort.Newest,
        _ => BrandSort.NameAsc
    };

    /// <summary>Copies the request onto the brand after checking that name and slug are free.</summary>
    private async Task ApplyAsync(Brand brand, BrandRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var slug = SlugGenerator.Generate(name, SlugMaxLength);

        // Brand names are unique, so a clashing slug ("G.Skill" vs "G Skill") is a conflict, not a suffix.
        if (await unitOfWork.Brands.ExistsAsync(b => b.Id != brand.Id && (b.Name == name || b.Slug == slug), cancellationToken))
        {
            throw new ConflictException($"A brand named '{name}' (or with the slug '{slug}') already exists.");
        }

        brand.Name = name;
        brand.Slug = slug;
        brand.LogoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
    }
}
