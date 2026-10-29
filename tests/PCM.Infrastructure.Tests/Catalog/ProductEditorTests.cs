using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.Catalog;
using PCM.Infrastructure.DTO.Catalog;
using PCM.Infrastructure.Exceptions;

namespace PCM.Infrastructure.Tests.Catalog;

public sealed class ProductEditorTests
{
    // RAM-like category: Memory Type (Enum, required), Capacity (Number, required), Notes (Text).
    private static readonly List<SpecAttribute> Attributes =
    [
        new()
        {
            Id = 10, Name = "Memory Type", Slug = "memory-type", DataType = "Enum", IsRequired = true,
            SpecOptions = [new SpecOption { Id = 100, Value = "DDR4", Slug = "ddr4" }, new SpecOption { Id = 101, Value = "DDR5", Slug = "ddr5" }]
        },
        new() { Id = 11, Name = "Capacity", Slug = "capacity", DataType = "Number", Unit = "GB", IsRequired = true },
        new() { Id = 12, Name = "Notes", Slug = "notes", DataType = "Text" }
    ];

    [Fact]
    public void Valid_specs_pass()
    {
        List<SpecValueRequest> specs =
        [
            new() { AttributeId = 10, OptionId = 101 },
            new() { AttributeId = 11, NumberValue = 32 },
            new() { AttributeId = 12, TextValue = "Low profile" }
        ];

        ProductEditor.ValidateSpecs(specs, Attributes);
    }

    [Fact]
    public void Attribute_of_another_category_is_rejected()
    {
        var errors = Validate([new() { AttributeId = 10, OptionId = 100 }, new() { AttributeId = 11, NumberValue = 16 }, new() { AttributeId = 99, NumberValue = 1 }]);

        Assert.Contains("specs[2].attributeId", errors.Keys);
    }

    [Fact]
    public void Option_of_another_attribute_is_rejected()
    {
        var errors = Validate([new() { AttributeId = 10, OptionId = 555 }, new() { AttributeId = 11, NumberValue = 16 }]);

        Assert.Contains("specs[0].optionId", errors.Keys);
    }

    [Fact]
    public void Value_of_the_wrong_type_is_rejected()
    {
        var errors = Validate([new() { AttributeId = 10, OptionId = 100 }, new() { AttributeId = 11, TextValue = "sixteen" }]);

        Assert.Contains("specs[1].numberValue", errors.Keys);
    }

    [Fact]
    public void Missing_required_attributes_are_listed()
    {
        var errors = Validate([new() { AttributeId = 12, TextValue = "Only notes" }]);

        var message = Assert.Single(errors["specs"]);
        Assert.Contains("Memory Type", message);
        Assert.Contains("Capacity", message);
    }

    private static IReadOnlyDictionary<string, string[]> Validate(List<SpecValueRequest> specs) =>
        Assert.Throws<RequestValidationException>(() => ProductEditor.ValidateSpecs(specs, Attributes)).Errors;
}
