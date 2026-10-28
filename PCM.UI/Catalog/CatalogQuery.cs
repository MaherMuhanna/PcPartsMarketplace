using System.Text;
using Microsoft.AspNetCore.Http;

namespace PCM.UI.Catalog;

/// <summary>
/// The catalog page's query string in canonical form, e.g.
/// <c>category=ram&amp;brand=corsair,kingston&amp;memory-type=ddr5&amp;capacity=32-64</c>.
/// The filter form submits checkboxes as repeated keys and number ranges as two inputs
/// ("capacity.min", "capacity.max"); <see cref="FromRequest"/> folds them into the short form,
/// which is also exactly what PCM.API expects, so the UI forwards it unchanged.
/// </summary>
public sealed class CatalogQuery
{
    public const string RangeMinSuffix = ".min";
    public const string RangeMaxSuffix = ".max";

    /// <summary>Keys with one free-text value (never split on commas, case kept).</summary>
    private static readonly HashSet<string> SingleValueKeys =
        new(["q", "category", "sort", "minPrice", "maxPrice", "page", "pageSize"], StringComparer.OrdinalIgnoreCase);

    private readonly List<KeyValuePair<string, string>> _pairs;

    private CatalogQuery(List<KeyValuePair<string, string>> pairs) => _pairs = pairs;

    public static CatalogQuery Empty { get; } = new([]);

    /// <summary>
    /// Canonical form of the request: empty values dropped, repeated and comma-separated values
    /// merged, trimmed, lower-cased and de-duplicated, ".min"/".max" pairs folded into "min-max".
    /// </summary>
    public static CatalogQuery FromRequest(IQueryCollection query)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        var ranges = new Dictionary<string, (string Min, string Max)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (rawKey, values) in query)
        {
            var key = rawKey.Trim();
            if (key.Length == 0)
            {
                continue;
            }

            if (TrySplitRangeKey(key, out var baseKey, out var isMin))
            {
                var value = values.FirstOrDefault()?.Trim() ?? string.Empty;
                var range = ranges.GetValueOrDefault(baseKey, (string.Empty, string.Empty));
                ranges[baseKey] = isMin ? (value, range.Max) : (range.Min, value);
                continue;
            }

            var canonical = SingleValueKeys.Contains(key)
                ? values.FirstOrDefault()?.Trim() ?? string.Empty
                : string.Join(',', values
                    .SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Select(v => v.ToLowerInvariant())
                    .Distinct());

            Set(pairs, key, canonical);
        }

        foreach (var (key, (min, max)) in ranges)
        {
            Set(pairs, key, min.Length == 0 && max.Length == 0 ? string.Empty : $"{min}-{max}");
        }

        return new CatalogQuery(pairs);
    }

    public string? Get(string key) =>
        _pairs.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value;

    /// <summary>Comma-separated values of a key as a set (brand slugs, option slugs).</summary>
    public IReadOnlySet<string> GetSet(string key) =>
        (Get(key) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The two halves of a "min-max" value, for the range inputs.</summary>
    public (string Min, string Max) GetRange(string key)
    {
        var value = Get(key);
        if (string.IsNullOrEmpty(value))
        {
            return (string.Empty, string.Empty);
        }

        var dash = value.IndexOf('-');
        return dash < 0 ? (value, value) : (value[..dash], value[(dash + 1)..]);
    }

    public bool Has(string key) => !string.IsNullOrEmpty(Get(key));

    /// <summary>A copy with the key set (or removed when the value is null or empty).</summary>
    public CatalogQuery With(string key, string? value)
    {
        var pairs = new List<KeyValuePair<string, string>>(_pairs);
        Set(pairs, key, value ?? string.Empty);
        return new CatalogQuery(pairs);
    }

    /// <summary>A copy without the given keys.</summary>
    public CatalogQuery Without(params string[] keys) =>
        new(_pairs.Where(p => !keys.Contains(p.Key, StringComparer.OrdinalIgnoreCase)).ToList());

    /// <summary>Spec filter keys (everything that is not a known catalog key).</summary>
    public IEnumerable<string> SpecKeys =>
        _pairs.Select(p => p.Key).Where(k => !SingleValueKeys.Contains(k) && !string.Equals(k, "brand", StringComparison.OrdinalIgnoreCase));

    /// <summary>Query string without the leading '?'. Commas stay readable; everything else is escaped.</summary>
    public string ToQueryString()
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in _pairs)
        {
            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value).Replace("%2C", ",", StringComparison.Ordinal));
        }

        return builder.ToString();
    }

    /// <summary>"/Catalog?..." link for this query.</summary>
    public string ToUrl(string path)
    {
        var queryString = ToQueryString();
        return queryString.Length == 0 ? path : $"{path}?{queryString}";
    }

    // ---------------------------------------------------------------------

    private static bool TrySplitRangeKey(string key, out string baseKey, out bool isMin)
    {
        isMin = key.EndsWith(RangeMinSuffix, StringComparison.OrdinalIgnoreCase);
        var isMax = key.EndsWith(RangeMaxSuffix, StringComparison.OrdinalIgnoreCase);
        baseKey = isMin || isMax ? key[..^RangeMinSuffix.Length] : key;
        return (isMin || isMax) && baseKey.Length > 0;
    }

    /// <summary>Replaces the key in place (keeps its position) or appends it; an empty value removes it.</summary>
    private static void Set(List<KeyValuePair<string, string>> pairs, string key, string value)
    {
        var index = pairs.FindIndex(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        if (value.Length == 0)
        {
            if (index >= 0)
            {
                pairs.RemoveAt(index);
            }

            return;
        }

        if (index >= 0)
        {
            pairs[index] = new(key, value);
        }
        else
        {
            pairs.Add(new(key, value));
        }
    }
}
