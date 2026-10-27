using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.DTO.Addresses;

/// <summary>Shipping address as returned by the API.</summary>
public sealed record AddressDto(
    int Id,
    string City,
    string Street,
    string? Details,
    string Phone,
    bool IsDefault);

/// <summary>POST /api/v1/account/addresses. The first address always becomes the default.</summary>
public sealed record CreateAddressRequest
{
    public string City { get; init; } = string.Empty;

    public string Street { get; init; } = string.Empty;

    /// <summary>Building, floor, landmark.</summary>
    public string? Details { get; init; }

    public string Phone { get; init; } = string.Empty;

    public bool IsDefault { get; init; }
}

/// <summary>PUT /api/v1/account/addresses/{id}. Use PUT .../{id}/default to change the default.</summary>
public sealed record UpdateAddressRequest
{
    public string City { get; init; } = string.Empty;

    public string Street { get; init; } = string.Empty;

    public string? Details { get; init; }

    public string Phone { get; init; } = string.Empty;
}

/// <summary>GET /api/v1/account/addresses query. Order is fixed: default first, then newest.</summary>
public sealed class AddressListQuery : PageQuery
{
    public static readonly string[] AllowedSorts = ["newest"];
}
