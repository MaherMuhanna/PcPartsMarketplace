using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Repositories.Common;

namespace PCM.Repositories.IRepository;

/// <summary>Sort orders supported by the admin seller list.</summary>
public enum SellerSort
{
    Newest,
    Oldest,
    StoreName
}

public interface ISellerProfileRepository : IRepository<SellerProfile>
{
    /// <summary>Tracked profile of the given seller user, or null.</summary>
    Task<SellerProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Current status of the given seller user (read-only), or null if the user has no profile.</summary>
    Task<SellerStatus?> GetStatusByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Read-only page of profiles, optionally filtered by status.</summary>
    Task<PagedResult<SellerProfile>> GetPagedAsync(
        SellerStatus? status, int page, int pageSize, SellerSort sort, CancellationToken cancellationToken = default);
}
