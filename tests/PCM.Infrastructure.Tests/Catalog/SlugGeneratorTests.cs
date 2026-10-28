using PCM.Infrastructure.Catalog;

namespace PCM.Infrastructure.Tests.Catalog;

public sealed class SlugGeneratorTests
{
    [Theory]
    [InlineData("AMD Ryzen 7 7800X3D", "amd-ryzen-7-7800x3d")]
    [InlineData("G.Skill", "g-skill")]
    [InlineData("  Corsair Vengeance DDR5 32GB (2x16GB) 6000MT/s CL30  ", "corsair-vengeance-ddr5-32gb-2x16gb-6000mt-s-cl30")]
    [InlineData("80+ Gold", "80-gold")]
    [InlineData("2.5\"", "2-5")]
    [InlineData("WD_BLACK SN850X", "wd-black-sn850x")]
    [InlineData("Café Résumé", "cafe-resume")]
    [InlineData("---", SlugGenerator.Fallback)]
    public void Generate_builds_lower_case_dash_separated_slugs(string text, string expected)
    {
        Assert.Equal(expected, SlugGenerator.Generate(text));
    }

    [Fact]
    public void Generate_truncates_without_a_trailing_dash()
    {
        Assert.Equal("abc", SlugGenerator.Generate("abc def", maxLength: 4));
    }

    [Theory]
    [InlineData(1, "ryzen")]
    [InlineData(2, "ryzen-2")]
    [InlineData(12, "ryzen-12")]
    public void WithSuffix_appends_the_attempt_number_from_the_second_try(int attempt, string expected)
    {
        Assert.Equal(expected, SlugGenerator.WithSuffix("ryzen", attempt, maxLength: 100));
    }

    [Fact]
    public void WithSuffix_keeps_the_result_within_max_length()
    {
        var slug = SlugGenerator.WithSuffix("abcdefghij", attempt: 3, maxLength: 10);

        Assert.Equal("abcdefgh-3", slug);
        Assert.True(slug.Length <= 10);
    }
}
