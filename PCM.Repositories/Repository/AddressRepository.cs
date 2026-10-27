using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.Repositories.Common;
using PCM.Repositories.IRepository;

namespace PCM.Repositories.Repository;

public class AddressRepository(PcPartsContext context) : Repository<Address>(context), IAddressRepository
{
    public Task<PagedResult<Address>> GetPagedByUserAsync(int userId, int page, int pageSize, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .ToPagedResultAsync(page, pageSize, cancellationToken);

    public Task<Address?> GetOwnedAsync(int id, int userId, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, cancellationToken);

    public Task<Address?> GetDefaultAsync(int userId, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault, cancellationToken);

    public Task<Address?> GetNewestOtherAsync(int userId, int excludeId, CancellationToken cancellationToken = default) =>
        DbSet.Where(a => a.UserId == userId && a.Id != excludeId)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
