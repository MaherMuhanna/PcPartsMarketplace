using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using PCM.UI.Catalog;

namespace PCM.UI.Tests;

/// <summary>The catalog form's query string is folded into one short canonical URL (also what the API expects).</summary>
public sealed class CatalogQueryTests
{
    private static CatalogQuery Parse(params (string Key, string[] Values)[] pairs) =>
        CatalogQuery.FromRequest(new QueryCollection(pairs.ToDictionary(p => p.Key, p => new StringValues(p.Values))));

    [Fact]
    public void Repeated_checkbox_keys_are_merged_lower_cased_and_deduplicated()
    {
        var query = Parse(("brand", ["Corsair", "kingston", "corsair"]), ("memory-type", ["DDR5"]));

        Assert.Equal("brand=corsair,kingston&memory-type=ddr5", query.ToQueryString());
    }

    [Fact]
    public void Comma_separated_and_repeated_values_give_the_same_result()
    {
        var repeated = Parse(("capacity", ["32", "64"]));
        var commas = Parse(("capacity", ["32, 64,"]));

        Assert.Equal(repeated.ToQueryString(), commas.ToQueryString());
        Assert.Equal("capacity=32,64", commas.ToQueryString());
    }

    [Fact]
    public void Empty_values_are_dropped()
    {
        var query = Parse(("q", [""]), ("brand", ["", " "]), ("minPrice", ["  "]), ("category", ["ram"]));

        Assert.Equal("category=ram", query.ToQueryString());
        Assert.False(query.Has("q"));
    }

    [Fact]
    public void Single_value_keys_keep_their_case_and_are_not_split()
    {
        var query = Parse(("q", [" Vengeance 32GB, DDR5 "]), ("sort", ["-price"]));

        Assert.Equal("Vengeance 32GB, DDR5", query.Get("q"));
        Assert.Equal("q=Vengeance%2032GB,%20DDR5&sort=-price", query.ToQueryString());
    }

    [Fact]
    public void With_replaces_in_place_and_empty_removes()
    {
        var query = Parse(("category", ["ram"]), ("page", ["3"]), ("sort", ["name"]));

        Assert.Equal("category=ram&page=1&sort=name", query.With("page", "1").ToQueryString());
        Assert.Equal("category=ram&sort=name", query.With("page", null).ToQueryString());
        Assert.Equal("category=ram&page=3&sort=name&brand=amd", query.With("brand", "amd").ToQueryString());
    }

    [Fact]
    public void Spec_keys_exclude_known_catalog_keys()
    {
        var query = Parse(("category", ["ram"]), ("brand", ["corsair"]), ("capacity", ["32"]), ("rgb", ["yes"]), ("page", ["2"]));

        Assert.Equal(new[] { "capacity", "rgb" }, query.SpecKeys);
        Assert.Equal(new[] { "corsair" }, query.GetSet("brand"));
    }

    [Fact]
    public void Range_values_split_on_the_dash()
    {
        var query = Parse(("capacity", ["16-64"]), ("speed", ["6000"]));

        Assert.Equal(("16", "64"), query.GetRange("capacity"));
        Assert.Equal(("6000", "6000"), query.GetRange("speed"));
        Assert.Equal((string.Empty, string.Empty), query.GetRange("missing"));
    }

    [Fact]
    public void Url_has_no_question_mark_when_empty()
    {
        Assert.Equal("/catalog", CatalogQuery.Empty.ToUrl("/catalog"));
        Assert.Equal("/catalog?category=gpu", Parse(("category", ["gpu"])).ToUrl("/catalog"));
    }
}
