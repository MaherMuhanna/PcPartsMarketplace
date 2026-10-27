using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Repositories;

namespace PCM.Infrastructure.Services;

/// <remarks>
/// The database allows one default per user (filtered unique index UX_Address_UserId_Default).
/// Changing the default therefore clears the old one and saves BEFORE setting the new one,
/// inside a transaction, so the index is never violated even for a moment.
/// </remarks>
public sealed class AddressService(IUnitOfWork unitOfWork) : IAddressService
{
    public async Task<PagedResponse<AddressDto>> GetAddressesAsync(int userId, AddressListQuery query, CancellationToken cancellationToken = default)
    {
        var page = await unitOfWork.Addresses.GetPagedByUserAsync(userId, query.Page, query.PageSize, cancellationToken);
        return page.ToResponse(address => address.ToDto());
    }

    public async Task<AddressDto> GetAddressAsync(int userId, int addressId, CancellationToken cancellationToken = default) =>
        (await GetOwnedOrThrowAsync(userId, addressId, cancellationToken)).ToDto();

    public async Task<AddressDto> CreateAddressAsync(int userId, CreateAddressRequest request, CancellationToken cancellationToken = default)
    {
        var isFirst = !await unitOfWork.Addresses.ExistsAsync(a => a.UserId == userId, cancellationToken);
        var makeDefault = request.IsDefault || isFirst;

        var address = new Address
        {
            UserId = userId,
            City = request.City.Trim(),
            Street = request.Street.Trim(),
            Details = NullIfBlank(request.Details),
            Phone = request.Phone.Trim(),
            IsDefault = makeDefault
        };

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (makeDefault)
        {
            await ClearDefaultAsync(userId, cancellationToken);
        }

        await unitOfWork.Addresses.AddAsync(address, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return address.ToDto();
    }

    public async Task<AddressDto> UpdateAddressAsync(int userId, int addressId, UpdateAddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = await GetOwnedOrThrowAsync(userId, addressId, cancellationToken);

        address.City = request.City.Trim();
        address.Street = request.Street.Trim();
        address.Details = NullIfBlank(request.Details);
        address.Phone = request.Phone.Trim();

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return address.ToDto();
    }

    public async Task DeleteAddressAsync(int userId, int addressId, CancellationToken cancellationToken = default)
    {
        var address = await GetOwnedOrThrowAsync(userId, addressId, cancellationToken);
        var wasDefault = address.IsDefault;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Soft delete (the interceptor sets IsDeleted = 1). Orders keep pointing at the row.
        address.IsDefault = false;
        unitOfWork.Addresses.Remove(address);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (wasDefault && await unitOfWork.Addresses.GetNewestOtherAsync(userId, addressId, cancellationToken) is { } next)
        {
            next.IsDefault = true;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetDefaultAddressAsync(int userId, int addressId, CancellationToken cancellationToken = default)
    {
        var address = await GetOwnedOrThrowAsync(userId, addressId, cancellationToken);
        if (address.IsDefault)
        {
            return;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await ClearDefaultAsync(userId, cancellationToken);

        address.IsDefault = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------

    private async Task<Address> GetOwnedOrThrowAsync(int userId, int addressId, CancellationToken cancellationToken) =>
        await unitOfWork.Addresses.GetOwnedAsync(addressId, userId, cancellationToken)
        ?? throw new NotFoundException($"Address {addressId} was not found.");

    /// <summary>Clears the current default (if any) and saves, so a new default can be set next.</summary>
    private async Task ClearDefaultAsync(int userId, CancellationToken cancellationToken)
    {
        var current = await unitOfWork.Addresses.GetDefaultAsync(userId, cancellationToken);
        if (current is null)
        {
            return;
        }

        current.IsDefault = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
