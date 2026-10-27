using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.Validators;

namespace PCM.Infrastructure.Tests.Validators;

public sealed class BrandListQueryValidatorTests
{
    private readonly BrandListQueryValidator _validator = new();

    [Fact]
    public void Defaults_are_valid()
    {
        Assert.True(_validator.Validate(new BrandListQuery()).IsValid);
    }

    [Theory]
    [InlineData(1, 1, "name")]
    [InlineData(5, 100, "-name")]
    [InlineData(1, 20, "Newest")]
    public void Valid_queries_pass(int page, int pageSize, string sort)
    {
        var query = new BrandListQuery { Page = page, PageSize = pageSize, Sort = sort };

        Assert.True(_validator.Validate(query).IsValid);
    }

    [Theory]
    [InlineData(0, 20, null, nameof(BrandListQuery.Page))]
    [InlineData(1, 0, null, nameof(BrandListQuery.PageSize))]
    [InlineData(1, 101, null, nameof(BrandListQuery.PageSize))]
    [InlineData(1, 20, "price", nameof(BrandListQuery.Sort))]
    public void Invalid_queries_fail_on_the_right_field(int page, int pageSize, string? sort, string expectedField)
    {
        var query = new BrandListQuery { Page = page, PageSize = pageSize, Sort = sort };

        var result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == expectedField);
    }
}
