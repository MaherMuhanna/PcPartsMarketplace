using Microsoft.AspNetCore.Mvc.Rendering;

namespace PCM.UI.Extensions;

/// <summary>Highlights the navbar entry of the current page.</summary>
public static class NavigationExtensions
{
    /// <summary>True when the current request is handled by one of <paramref name="controllers"/> (names without "Controller").</summary>
    public static bool IsController(this ViewContext viewContext, params string[] controllers)
    {
        ArgumentNullException.ThrowIfNull(viewContext);
        var current = viewContext.RouteData.Values["controller"] as string;
        return current is not null && controllers.Contains(current, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>"active" for the current page's link (Bootstrap class), otherwise an empty string.</summary>
    public static string ActiveIf(this ViewContext viewContext, params string[] controllers) =>
        viewContext.IsController(controllers) ? "active" : string.Empty;

    /// <summary>aria-current value for the current page's link; null removes the attribute.</summary>
    public static string? AriaCurrent(this ViewContext viewContext, params string[] controllers) =>
        viewContext.IsController(controllers) ? "page" : null;
}
