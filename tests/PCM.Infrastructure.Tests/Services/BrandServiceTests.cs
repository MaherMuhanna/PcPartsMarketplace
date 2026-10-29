using NSubstitute;
using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.Services;
using PCM.Repositories;
using PCM.Repositories.Common;
using PCM.Repositories.IRepository;

namespace PCM.Infrastructure.Tests.Services;

public sealed class BrandServiceTests
{
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly BrandService _service;

    public BrandServiceTests()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Brands.Returns(_brands);
        _service = new BrandService(unitOfWork);
    }

    [Fact]
    public async Task GetBrandsAsync_maps_entities_and_paging_metadata()
    {
        var page = new PagedResult<Brand>(
            [new Brand { Id = 1, Name = "AMD", Slug = "amd" }, new Brand { Id = 2, Name = "Corsair", Slug = "corsair", LogoUrl = "https://cdn/corsair.png" }],
            Page: 2, PageSize: 2, TotalCount: 5);
        _brands.GetPagedAsync(2, 2, BrandSort.NameAsc, Arg.Any<CancellationToken>()).Returns(page);

        var result = await _service.GetBrandsAsync(new BrandListQuery { Page = 2, PageSize = 2 }, TestContext.Current.CancellationToken);

        Assert.Equal([new BrandDto(1, "AMD", "amd", null), new BrandDto(2, "Corsair", "corsair", "https://cdn/corsair.png")], result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Theory]
    [InlineData(null, BrandSort.NameAsc)]
    [InlineData("name", BrandSort.NameAsc)]
    [InlineData("-name", BrandSort.NameDesc)]
    [InlineData("NEWEST", BrandSort.Newest)]
    public async Task GetBrandsAsync_translates_sort_key(string? sort, BrandSort expected)
    {
        _brands.GetPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<BrandSort>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<Brand>([], 1, 20, 0));

        await _service.GetBrandsAsync(new BrandListQuery { Sort = sort }, TestContext.Current.CancellationToken);

        await _brands.Received(1).GetPagedAsync(1, 20, expected, Arg.Any<CancellationToken>());
    }
}
