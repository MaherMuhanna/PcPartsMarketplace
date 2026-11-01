using System.ComponentModel.DataAnnotations;

namespace PCM.API.Caching;

/// <summary>"CatalogCache" configuration section: how long catalog responses may live without an eviction.</summary>
public sealed class CatalogCacheOptions
{
    public const string SectionName = "CatalogCache";

    /// <summary>Search, filters and product pages (agreed: 60 seconds).</summary>
    [Range(1, 3600)]
    public int ProductsSeconds { get; set; } = 60;

    /// <summary>Category tree, attributes and brands (agreed: 10 minutes).</summary>
    [Range(1, 86400)]
    public int CatalogSeconds { get; set; } = 600;
}

/// <summary>Registers output caching for the public catalog.</summary>
public static class CachingSetup
{
    /// <summary>Total memory the cache may use; least recently used entries go first.</summary>
    private const long CacheSizeLimitBytes = 64L * 1024 * 1024;

    /// <summary>A larger response (e.g. a huge page) is served but not cached.</summary>
    private const long MaxCachedBodyBytes = 1024 * 1024;

    /// <summary>
    /// In-memory output cache with the <see cref="CachePolicies.Catalog"/> and <see cref="CachePolicies.Products"/> policies.
    /// One API instance only; with several instances, swap in a shared store
    /// (AddStackExchangeRedisOutputCache) so an eviction reaches every instance.
    /// </summary>
    public static WebApplicationBuilder AddCatalogCaching(this WebApplicationBuilder builder)
    {
        // Read now (policies are built at startup) and validate like ValidateOnStart would. No secrets involved.
        var settings = builder.Configuration.GetSection(CatalogCacheOptions.SectionName).Get<CatalogCacheOptions>()
            ?? new CatalogCacheOptions();
        Validator.ValidateObject(settings, new ValidationContext(settings), validateAllProperties: true);

        builder.Services.AddOutputCache(options =>
        {
            options.SizeLimit = CacheSizeLimitBytes;
            options.MaximumBodySize = MaxCachedBodyBytes;
            options.AddPolicy(CachePolicies.Catalog,
                new PublicCatalogCachePolicy(TimeSpan.FromSeconds(settings.CatalogSeconds), CacheTags.Catalog));
            options.AddPolicy(CachePolicies.Products,
                new PublicCatalogCachePolicy(TimeSpan.FromSeconds(settings.ProductsSeconds), CacheTags.Products));
        });
        builder.Services.AddSingleton<CatalogCacheInvalidator>();

        return builder;
    }
}
