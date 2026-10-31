using System.Net;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PCM.Infrastructure.DTO.Catalog;
using PCM.UI.ApiClients;
using PCM.UI.Catalog;
using PCM.UI.Extensions;
using PCM.UI.Models;

namespace PCM.UI.Tests;

/// <summary>
/// The shared product form sends only filled specs and image rows, so API list indexes differ from
/// form rows; errors must still land next to the right input.
/// </summary>
public sealed class ProductFormTests
{
    private static SpecFieldViewModel Field(int attributeId, string dataType, string? text = null, decimal? number = null, int? option = null) =>
        new() { AttributeId = attributeId, DataType = dataType, TextValue = text, NumberValue = number, OptionId = option };

    private static ProductFormViewModel Form() => new()
    {
        CategoryId = 4,
        BrandId = 2,
        Name = "  Corsair Vengeance 32GB  ",
        ModelNumber = " CMK32GX5M2B6000C36 ",
        Description = "   ",
        Specs =
        [
            Field(10, "Enum", option: 100),     // row 0 -> specs[0]
            Field(11, "Number"),                // row 1: empty, skipped
            Field(12, "Text", text: "  "),      // row 2: blank, skipped
            Field(13, "Number", number: 6000m), // row 3 -> specs[1]
            Field(14, "Text", text: " CL36 ")   // row 4 -> specs[2]
        ],
        Images =
        [
            new ImageRowViewModel { Url = "" },                                         // row 0: skipped
            new ImageRowViewModel { Url = " /images/products/ram.svg ", AltText = " " }  // row 1 -> images[0]
        ]
    };

    [Fact]
    public void Request_has_trimmed_texts_and_only_filled_specs_and_images()
    {
        var built = ProductFormBuilder.ToRequest(Form());
        var request = built.Request;

        Assert.Equal(("Corsair Vengeance 32GB", "CMK32GX5M2B6000C36", (string?)null), (request.Name, request.ModelNumber, request.Description));
        Assert.Equal(new[] { 10, 13, 14 }, request.Specs.Select(s => s.AttributeId));
        Assert.Equal(100, request.Specs[0].OptionId);
        Assert.Equal((6000m, (string?)null, (int?)null), (request.Specs[1].NumberValue!.Value, request.Specs[1].TextValue, request.Specs[1].OptionId));
        Assert.Equal("CL36", request.Specs[2].TextValue);

        var image = Assert.Single(request.Images);
        Assert.Equal(("/images/products/ram.svg", (string?)null), (image.Url, image.AltText));

        Assert.Equal(new[] { 0, 3, 4 }, built.SpecRows);
        Assert.Equal(new[] { 1 }, built.ImageRows);
    }

    [Fact]
    public void Api_errors_are_mapped_back_to_form_rows()
    {
        var built = ProductFormBuilder.ToRequest(Form());
        var problem = new ApiProblem
        {
            Status = 400,
            Errors = new Dictionary<string, string[]>
            {
                ["specs[1].numberValue"] = ["Too big."],
                ["specs[2]"] = ["Set exactly one value."],
                ["images[0].url"] = ["Not an image URL."],
                ["specs"] = ["Capacity is required."],
                ["modelNumber"] = ["Already used for this brand."]
            }
        };
        var modelState = new ModelStateDictionary();

        var shown = ProductFormBuilder.TryAddApiErrors(modelState, new ApiException(HttpStatusCode.BadRequest, problem), built);

        Assert.True(shown);
        Assert.Equal(new[] { "Too big." }, modelState.ErrorsFor("Specs[3]"));
        Assert.Equal(new[] { "Set exactly one value." }, modelState.ErrorsFor("Specs[4]"));
        Assert.Equal(new[] { "Not an image URL." }, modelState.ErrorsFor("Images[1]"));
        Assert.Equal(new[] { "Capacity is required." }, modelState.ErrorsFor("Specs"));
        Assert.Equal(new[] { "Already used for this brand." }, modelState.ErrorsFor("ModelNumber"));
        Assert.Empty(modelState.ErrorsFor("Specs[1]"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public void Errors_the_form_cannot_show_are_left_to_the_exception_filter(HttpStatusCode status)
    {
        var shown = ProductFormBuilder.TryAddApiErrors(new ModelStateDictionary(), new ApiException(status, null), ProductFormBuilder.ToRequest(Form()));

        Assert.False(shown);
    }

    [Fact]
    public void Conflict_becomes_a_form_level_message()
    {
        var modelState = new ModelStateDictionary();
        var problem = new ApiProblem { Status = 409, Detail = "Approved proposals cannot be edited." };

        ProductFormBuilder.TryAddApiErrors(modelState, new ApiException(HttpStatusCode.Conflict, problem), ProductFormBuilder.ToRequest(Form()));

        Assert.Equal(new[] { "Approved proposals cannot be edited." }, modelState.ErrorsFor(string.Empty));
    }

    [Fact]
    public void Spec_fields_follow_the_attribute_order_and_keep_matching_values()
    {
        SpecAttributeDto Attribute(int id, string name, int sort, string type, params SpecOptionDto[] options) =>
            new(id, 4, name, name.ToLowerInvariant(), type, null, false, false, sort, options);

        var attributes = new[]
        {
            Attribute(2, "Speed", 2, "Number"),
            Attribute(1, "Type", 1, "Enum", new SpecOptionDto(7, "DDR5", "ddr5", 2), new SpecOptionDto(6, "DDR4", "ddr4", 1))
        };
        var posted = new[]
        {
            new SpecFieldViewModel { AttributeId = 2, NumberValue = 6000m },
            new SpecFieldViewModel { AttributeId = 1, OptionId = 99 },          // not an option of the attribute
            new SpecFieldViewModel { AttributeId = 50, TextValue = "stale" }     // attribute of another category
        };

        var fields = SpecFieldViewModel.For(attributes, posted);

        Assert.Equal(new[] { "Type", "Speed" }, fields.Select(f => f.Name));
        Assert.Equal(new[] { "DDR4", "DDR5" }, fields[0].Options.Select(o => o.Value));
        Assert.Null(fields[0].OptionId);
        Assert.Equal(6000m, fields[1].NumberValue);
    }

    [Theory]
    [InlineData("https://cdn.example.com/a.png", true)]
    [InlineData("/images/products/ram.svg", true)]
    [InlineData("//evil.example.com/a.png", false)]
    [InlineData("/\\evil.example.com", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    public void Image_preview_uses_the_same_rule_as_the_api(string url, bool expected)
    {
        Assert.Equal(expected, new ImageRowViewModel { Url = url }.IsPreviewable);
    }

    [Theory]
    [InlineData(1, 1, "1")]
    [InlineData(1, 5, "1,2,3,4,5")]
    [InlineData(1, 10, "1,2,3,0,10")]
    [InlineData(6, 12, "1,0,4,5,6,7,8,0,12")]
    [InlineData(12, 12, "1,0,10,11,12")]
    public void Pager_collapses_long_ranges(int page, int totalPages, string expected)
    {
        var pager = PagerViewModel.Create(page, totalPages, "Index", new { status = "Pending", q = (string?)null });

        Assert.Equal(expected, string.Join(',', pager.Pages()));
        Assert.Equal(new Dictionary<string, string> { ["status"] = "Pending", ["page"] = "2" }, pager.For(2));
    }
}
