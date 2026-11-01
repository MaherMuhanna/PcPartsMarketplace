using Microsoft.AspNetCore.OutputCaching;

namespace PCM.API.Caching;

/// <summary>Which cached catalog responses a write makes stale.</summary>
public enum CatalogCacheScope
{
    /// <summary>Prices, stock, offers or product data changed: search, filters and product pages.</summary>
    Products,

    /// <summary>Categories, attributes, options or brands changed: everything, product pages included.</summary>
    All
}

/// <summary>
/// Drops stale catalog responses from the output cache after a successful write.
/// Eviction is the main freshness mechanism; the short expiry of each policy is only a safety net
/// (e.g. a read that started before a write commits and stores its result just after the eviction).
/// </summary>
public sealed class CatalogCacheInvalidator(IOutputCacheStore store, ILogger<CatalogCacheInvalidator> logger)
{
    /// <summary>Evicts the tags of <paramref name="scope"/>. Never throws: the write has already committed.</summary>
    /// <param name="scope">What changed.</param>
    /// <param name="cancellationToken">Normally <see cref="CancellationToken.None"/>: a client that disconnects must not keep stale data cached.</param>
    public async Task EvictAsync(CatalogCacheScope scope, CancellationToken cancellationToken = default)
    {
        try
        {
            await store.EvictByTagAsync(CacheTags.Products, cancellationToken);
            if (scope == CatalogCacheScope.All)
            {
                await store.EvictByTagAsync(CacheTags.Catalog, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // In-memory eviction does not fail; a distributed store (Redis) could. The expiry still bounds staleness.
            logger.LogWarning(ex, "Evicting the catalog output cache ({Scope}) failed", scope);
        }
    }
}
