using PCM.Infrastructure.DTO.Dashboards;
using PCM.Infrastructure.DTO.Sellers;

namespace PCM.UI.Models;

/// <summary>Seller home: the live store profile, plus the numbers when the seller is approved.</summary>
public sealed class SellerDashboardViewModel
{
    public required SellerProfileDto Profile { get; init; }

    /// <summary>Null when the seller is not approved (the page shows the store-status card instead).</summary>
    public SellerDashboardDto? Dashboard { get; init; }
}

/// <summary>Input of the _StatCard partial.</summary>
/// <param name="Label">What is counted.</param>
/// <param name="Value">Formatted number or amount.</param>
/// <param name="Url">List behind the number (null = not a link).</param>
/// <param name="Hint">Small text under the number.</param>
/// <param name="Highlight">Draws attention (something waits for action).</param>
/// <param name="ValueCss">Extra CSS class of the number (e.g. text-danger).</param>
public sealed record StatCardViewModel(
    string Label,
    string Value,
    string? Url = null,
    string? Hint = null,
    bool Highlight = false,
    string? ValueCss = null);
