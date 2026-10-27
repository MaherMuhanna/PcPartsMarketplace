using PCM.DomainEntities.DBEntities;

namespace PCM.Repositories.IRepository;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>Read-only lookup by SHA-256 hash (the raw token is never stored).</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Tracked tokens of the user that are neither revoked nor expired.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(int userId, DateTime utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically revokes the token only if it is still active, in a single
    /// UPDATE ... WHERE RevokedAt IS NULL statement. Returns false when another
    /// request revoked it first, which is how concurrent reuse of the same refresh
    /// token is detected. Runs immediately (not staged): callers that need it to be
    /// atomic with other changes must call it inside a Unit of Work transaction.
    /// </summary>
    Task<bool> TryRevokeAsync(int id, DateTime utcNow, int? replacedByTokenId, CancellationToken cancellationToken = default);
}
