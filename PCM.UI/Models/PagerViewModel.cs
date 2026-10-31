namespace PCM.UI.Models;

/// <summary>
/// Input of the shared _Pager partial: current page, page count, and the route values to keep
/// (filters, sort) on every page link. Links point at the current controller's <see cref="Action"/>.
/// </summary>
public sealed record PagerViewModel(int Page, int TotalPages, string Action, IDictionary<string, string> RouteValues)
{
    /// <summary>Pages shown on each side of the current one before "..." is used.</summary>
    public const int Window = 2;

    public static PagerViewModel Create(int page, int totalPages, string action, object? routeValues = null)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (routeValues is not null)
        {
            foreach (var property in routeValues.GetType().GetProperties())
            {
                var value = property.GetValue(routeValues)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    values[property.Name] = value;
                }
            }
        }

        return new PagerViewModel(page, totalPages, action, values);
    }

    /// <summary>Route values of one page link: the kept values plus page.</summary>
    public IDictionary<string, string> For(int page) =>
        new Dictionary<string, string>(RouteValues, StringComparer.OrdinalIgnoreCase) { ["page"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    /// <summary>Page numbers to show: first, last and <see cref="Window"/> around the current one; 0 means a gap ("...").</summary>
    public IEnumerable<int> Pages()
    {
        var previous = 0;
        for (var p = 1; p <= TotalPages; p++)
        {
            if (p == 1 || p == TotalPages || Math.Abs(p - Page) <= Window)
            {
                // A gap of one page shows that page: "..." must save space.
                if (previous != 0 && p - previous == 2)
                {
                    yield return previous + 1;
                }
                else if (previous != 0 && p - previous > 2)
                {
                    yield return 0;
                }

                yield return p;
                previous = p;
            }
        }
    }
}
