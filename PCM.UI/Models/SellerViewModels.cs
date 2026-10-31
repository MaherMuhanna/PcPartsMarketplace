using System.ComponentModel.DataAnnotations;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Sellers;
using PCM.Infrastructure.Validators;

namespace PCM.UI.Models;

/// <summary>Admin > Sellers page.</summary>
public sealed class AdminSellersViewModel
{
    /// <summary>Status tabs, in display order (null = all).</summary>
    public static readonly string?[] StatusTabs = [null, "Pending", "Approved", "Suspended", "Rejected"];

    public PagedResponse<SellerAdminDto> Sellers { get; init; } = new([], 1, 20, 0, 0);

    public string? Status { get; init; }
}

/// <summary>The seller's "My store" page: current status plus the editable fields.</summary>
public sealed class StoreViewModel
{
    public SellerProfileDto? Profile { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Contact phone"), RegularExpression(CommonRules.PhonePattern, ErrorMessage = "Phone must be 7 to 15 digits, optionally starting with +.")]
    public string? ContactPhone { get; set; }
}

/// <summary>The seller's product proposals: status tabs, sort and paging.</summary>
public sealed class SellerProposalsViewModel
{
    public static readonly string?[] StatusTabs = [null, "Pending", "Approved", "Rejected"];

    public PagedResponse<PCM.Infrastructure.DTO.Catalog.ProductManageListItemDto> Proposals { get; init; } = new([], 1, 20, 0, 0);

    public string? Status { get; init; }

    public string? Sort { get; init; }
}
