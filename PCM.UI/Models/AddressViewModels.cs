using System.ComponentModel.DataAnnotations;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.Validators;

namespace PCM.UI.Models;

/// <summary>Create and edit form for an address (Id is null when creating).</summary>
public sealed class AddressFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(100, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 2)]
    public string Street { get; set; } = string.Empty;

    [StringLength(200), Display(Name = "Details (building, floor, landmark)")]
    public string? Details { get; set; }

    [Required, RegularExpression(CommonRules.PhonePattern, ErrorMessage = "Phone must be 7 to 15 digits, optionally starting with +.")]
    public string Phone { get; set; } = string.Empty;

    [Display(Name = "Use as my default address")]
    public bool IsDefault { get; set; }

    public bool IsEdit => Id is not null;

    public static AddressFormViewModel From(AddressDto address) => new()
    {
        Id = address.Id,
        City = address.City,
        Street = address.Street,
        Details = address.Details,
        Phone = address.Phone,
        IsDefault = address.IsDefault
    };
}

public sealed class AddressListViewModel
{
    public IReadOnlyList<AddressDto> Addresses { get; init; } = [];
}
