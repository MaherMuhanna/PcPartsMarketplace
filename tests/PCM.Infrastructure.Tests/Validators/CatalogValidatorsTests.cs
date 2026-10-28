using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Validators;

namespace PCM.Infrastructure.Tests.Validators;

public sealed class CatalogValidatorsTests
{
    private static ProductUpsertRequest ValidProduct() => new()
    {
        CategoryId = 1,
        BrandId = 1,
        Name = "Corsair Vengeance 32GB",
        ModelNumber = "CMK32GX5M2B6000C30",
        Specs = [new SpecValueRequest { AttributeId = 1, NumberValue = 32 }],
        Images = [new ProductImageRequest { Url = "/images/products/ram.svg" }, new ProductImageRequest { Url = "https://cdn.example.com/ram.png" }]
    };

    [Fact]
    public void Valid_product_passes()
    {
        Assert.True(new ProductUpsertRequestValidator().Validate(ValidProduct()).IsValid);
    }

    [Fact]
    public void Spec_must_set_exactly_one_value()
    {
        var request = ValidProduct() with { Specs = [new SpecValueRequest { AttributeId = 1, NumberValue = 32, OptionId = 4 }] };

        var result = new ProductUpsertRequestValidator().Validate(request);

        Assert.Contains(result.Errors, e => e.PropertyName == "Specs[0].value");
    }

    [Fact]
    public void Duplicate_attributes_are_rejected()
    {
        var request = ValidProduct() with
        {
            Specs = [new SpecValueRequest { AttributeId = 1, NumberValue = 32 }, new SpecValueRequest { AttributeId = 1, NumberValue = 64 }]
        };

        Assert.Contains(new ProductUpsertRequestValidator().Validate(request).Errors, e => e.PropertyName == "Specs");
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example.com/x.png")]
    [InlineData("ftp://example.com/x.png")]
    [InlineData("")]
    public void Image_url_must_be_http_or_a_site_path(string url)
    {
        var request = ValidProduct() with { Images = [new ProductImageRequest { Url = url }] };

        Assert.Contains(new ProductUpsertRequestValidator().Validate(request).Errors, e => e.PropertyName == "Images[0].Url");
    }

    [Fact]
    public void At_most_eight_images()
    {
        var request = ValidProduct() with
        {
            Images = Enumerable.Range(1, 9).Select(i => new ProductImageRequest { Url = $"/images/{i}.png" }).ToList()
        };

        Assert.Contains(new ProductUpsertRequestValidator().Validate(request).Errors, e => e.PropertyName == "Images");
    }

    [Fact]
    public void Model_number_must_be_ascii()
    {
        var request = ValidProduct() with { ModelNumber = "موديل-1" };

        Assert.Contains(new ProductUpsertRequestValidator().Validate(request).Errors, e => e.PropertyName == "ModelNumber");
    }

    [Theory]
    [InlineData("Text", true, false)]
    [InlineData("Number", true, true)]
    [InlineData("enum", true, true)]
    [InlineData("Color", false, false)]
    public void Attribute_type_and_filterable_rules(string dataType, bool isFilterable, bool expectedValid)
    {
        var request = new SpecAttributeRequest { Name = "Speed", DataType = dataType, IsFilterable = isFilterable };

        Assert.Equal(expectedValid, new SpecAttributeRequestValidator().Validate(request).IsValid);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(10.0, 20.0, true)]
    [InlineData(20.0, 10.0, false)]
    [InlineData(-1.0, null, false)]
    public void Price_range_rules(double? min, double? max, bool expectedValid)
    {
        var query = new ProductListQuery { MinPrice = (decimal?)min, MaxPrice = (decimal?)max };

        Assert.Equal(expectedValid, new ProductListQueryValidator().Validate(query).IsValid);
    }

    [Fact]
    public void Unknown_sort_is_rejected()
    {
        Assert.False(new ProductListQueryValidator().Validate(new ProductListQuery { Sort = "rating" }).IsValid);
    }
}
