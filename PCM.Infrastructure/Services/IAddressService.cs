using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.Services;

/// <summary>
/// The signed-in customer's address book. Every method takes the user id from
/// the JWT; an address of another user is reported as not found (404).
/// </summary>
public interface IAddressService
{
    Task<PagedResponse<AddressDto>> GetAddressesAsync(int userId, AddressListQuery query, CancellationToken cancellationToken = default);

    Task<AddressDto> GetAddressAsync(int userId, int addressId, CancellationToken cancellationToken = default);

    /// <summary>Adds an address. It becomes the default when requested, or when it is the user's first one.</summary>
    Task<AddressDto> CreateAddressAsync(int userId, CreateAddressRequest request, CancellationToken cancellationToken = default);

    Task<AddressDto> UpdateAddressAsync(int userId, int addressId, UpdateAddressRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the address. If it was the default, the newest remaining address becomes the default.</summary>
    Task DeleteAddressAsync(int userId, int addressId, CancellationToken cancellationToken = default);

    /// <summary>Makes the address the user's only default.</summary>
    Task SetDefaultAddressAsync(int userId, int addressId, CancellationToken cancellationToken = default);
}
