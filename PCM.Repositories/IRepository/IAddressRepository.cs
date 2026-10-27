using PCM.DomainEntities.DBEntities;
using PCM.Repositories.Common;

namespace PCM.Repositories.IRepository;

/// <summary>
/// Customer addresses. Every read is scoped to the owner: callers pass the
/// current user id from the JWT, and another user's address is simply "not found".
/// </summary>
public interface IAddressRepository : IRepository<Address>
{
    /// <summary>Read-only page of the user's addresses: default first, then newest.</summary>
    Task<PagedResult<Address>> GetPagedByUserAsync(int userId, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Tracked address if it exists and belongs to the user, otherwise null.</summary>
    Task<Address?> GetOwnedAsync(int id, int userId, CancellationToken cancellationToken = default);

    /// <summary>Tracked default address of the user, or null.</summary>
    Task<Address?> GetDefaultAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Tracked newest address of the user other than <paramref name="excludeId"/>, or null.</summary>
    Task<Address?> GetNewestOtherAsync(int userId, int excludeId, CancellationToken cancellationToken = default);
}
