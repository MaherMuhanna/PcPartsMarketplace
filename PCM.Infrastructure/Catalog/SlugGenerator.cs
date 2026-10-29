using System.Globalization;
using System.Text;

namespace PCM.Infrastructure.Catalog;

/// <summary>
/// Builds URL slugs: lower case ASCII letters and digits separated by single dashes,
/// e.g. "G.Skill Trident Z5 (2x16GB)" -> "g-skill-trident-z5-2x16gb".
/// Database/seed.sql uses the same rule, so seeded and API-created slugs look alike.
/// </summary>
public static class SlugGenerator
{
    /// <summary>Used when the text has no usable characters (e.g. only symbols).</summary>
    public const string Fallback = "item";

    public static string Generate(string text, int maxLength = 100)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);

        // Split accented letters into base letter + accent mark, so "é" becomes "e".
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingDash = false;

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                // One dash for any run of other characters, never at the start.
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(lower);
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }

        var slug = builder.Length > maxLength ? builder.ToString(0, maxLength).TrimEnd('-') : builder.ToString();
        return slug.Length == 0 ? Fallback : slug;
    }

    /// <summary>
    /// "slug" for the first try, then "slug-2", "slug-3", ... trimmed so the result
    /// still fits in <paramref name="maxLength"/>.
    /// </summary>
    public static string WithSuffix(string slug, int attempt, int maxLength)
    {
        if (attempt <= 1)
        {
            return slug;
        }

        var suffix = "-" + attempt.ToString(CultureInfo.InvariantCulture);
        var head = slug.Length + suffix.Length > maxLength ? slug[..(maxLength - suffix.Length)].TrimEnd('-') : slug;
        return head + suffix;
    }
}
