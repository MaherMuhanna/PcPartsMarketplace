using PCM.Infrastructure.Services;

namespace PCM.Infrastructure.Tests.Services;

public sealed class ProductCatalogServiceTests
{
    [Theory]
    [InlineData("16-64", "16", "64")]
    [InlineData("32-", "32", null)]
    [InlineData("-64", null, "64")]
    [InlineData("32", "32", "32")]
    [InlineData(" 4.2 - 5 ", "4.2", "5")]
    public void TryParseRange_accepts_closed_open_and_exact_ranges(string text, string? expectedMin, string? expectedMax)
    {
        Assert.True(ProductCatalogService.TryParseRange(text, out var min, out var max));
        Assert.Equal(Parse(expectedMin), min);
        Assert.Equal(Parse(expectedMax), max);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-")]
    [InlineData("abc")]
    [InlineData("1-2-3")]
    [InlineData("64-16")]
    [InlineData("1,5-2")]
    public void TryParseRange_rejects_malformed_text(string text)
    {
        Assert.False(ProductCatalogService.TryParseRange(text, out _, out _));
    }

    private static decimal? Parse(string? value) =>
        value is null ? null : decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
