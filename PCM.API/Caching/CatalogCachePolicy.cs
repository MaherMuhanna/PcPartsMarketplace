using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace PCM.API.Caching;

/// <summary>Names of the output-cache policies used by the public catalog controllers.</summary>
public static class CachePolicies
{
    /// <summary>Category tree, category attributes, brands: change rarely (admin edits only).</summary>
    public const string Catalog = "Catalog";

    /// <summary>Product search, sidebar filters, product pages: change with prices and stock.</summary>
    public const string Products = "Products";
}

/// <summary>Cache tags. Evicting a tag drops every cached response stored under it.</summary>
public static class CacheTags
{
    public const string Catalog = "catalog";
    public const string Products = "products";
}

/// <summary>
/// Output-cache policy for public, read-only catalog endpoints.
/// <para>
/// Why not the built-in default policy: it never caches a request that carries an
/// <c>Authorization</c> header, and the MVC UI sends the user's bearer token on every call.
/// Signed-in shoppers (most of the traffic) would always miss the cache. These endpoints are
/// anonymous and return the same body for everyone, so the header is safe to ignore here.
/// Never apply this policy to an endpoint whose response depends on the current user.
/// </para>
/// </summary>
public sealed class PublicCatalogCachePolicy(TimeSpan duration, string tag) : IOutputCachePolicy
{
    ValueTask IOutputCachePolicy.CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var method = context.HttpContext.Request.Method;
        var cacheable = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

        context.EnableOutputCaching = true;
        context.AllowCacheLookup = cacheable;
        context.AllowCacheStorage = cacheable;
        context.AllowLocking = true;               // concurrent misses for one key run the query once
        context.CacheVaryByRules.QueryKeys = "*";  // every filter, sort and page is its own entry
        context.ResponseExpirationTimeSpan = duration;
        context.Tags.Add(tag);

        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;

    ValueTask IOutputCachePolicy.ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;

        // Only successful bodies are stored: a 404 for a random slug or a 400 for a bad filter
        // is cheap to recompute and must not fill the cache. Never store a response setting a cookie.
        if (response.StatusCode != StatusCodes.Status200OK || !StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}
