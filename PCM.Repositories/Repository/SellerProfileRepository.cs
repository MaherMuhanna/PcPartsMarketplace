using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Repositories.Common;
using PCM.Repositories.IRepository;

namespace PCM.Repositories.Repository;

public class SellerProfileRepository(PcPartsContext context) : Repository<SellerProfile>(context), ISellerProfileRepository
{
    public Task<SellerProfile?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        DbSet.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

    public async Task<SellerStatus?> GetStatusByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var status = await DbSet.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return status is null ? null : Enum.Parse<SellerStatus>(status);
    }

    public Task<PagedResult<SellerProfile>> GetPagedAsync(
        SellerStatus? status, int page, int pageSize, SellerSort sort, CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (status is not null)
        {
            var value = status.Value.ToString();
            query = query.Where(s => s.Status == value);
        }

        // Id is the tie-breaker so paging is stable when sort keys are equal.
        var ordered = sort switch
        {
            SellerSort.Oldest => query.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id),
            SellerSort.StoreName => query.OrderBy(s => s.StoreName).ThenBy(s => s.Id),
            _ => query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id)
        };

        return ordered.ToPagedResultAsync(page, pageSize, cancellationToken);
    }
}
