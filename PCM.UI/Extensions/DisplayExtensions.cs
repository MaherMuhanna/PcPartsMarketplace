using System.Globalization;

namespace PCM.UI.Extensions;

/// <summary>Formatting shared by the catalog views.</summary>
public static class DisplayExtensions
{
    /// <summary>Prices are stored without a currency; the store sells in Jordanian dinars.</summary>
    public const string Currency = "JOD";

    /// <summary>Shown when a product has no image.</summary>
    public const string PlaceholderImage = "/images/products/placeholder.svg";

    public static string ToPrice(this decimal amount) =>
        $"{Currency} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    /// <summary>Range input value without trailing zeros ("16" not "16.0000").</summary>
    public static string ToInputNumber(this decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);
}
