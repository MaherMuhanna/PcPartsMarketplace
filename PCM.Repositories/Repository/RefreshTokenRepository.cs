using Microsoft.EntityFrameworkCore;
using PCM.DomainEntities.Context;
using PCM.DomainEntities.DBEntities;
using PCM.Repositories.IRepository;

namespace PCM.Repositories.Repository;

public class RefreshTokenRepository(PcPartsContext context) : Repository<RefreshToken>(context), IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(int userId, DateTime utcNow, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > utcNow)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryRevokeAsync(int id, DateTime utcNow, int? replacedByTokenId, CancellationToken cancellationToken = default)
    {
        // ExecuteUpdate is a direct, set-based statement: it bypasses the change
        // tracker on purpose so the "still active" check and the update are one atomic step.
        var affected = await DbSet
            .Where(t => t.Id == id && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.RevokedAt, (DateTime?)utcNow)
                .SetProperty(t => t.ReplacedByTokenId, replacedByTokenId),
                cancellationToken);

        return affected == 1;
    }
}
