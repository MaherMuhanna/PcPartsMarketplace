using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace PCM.API.Caching;

/// <summary>
/// Put on a controller (or action) whose writes change what the public catalog shows.
/// After a successful non-GET action it evicts the cached catalog responses of <see cref="Scope"/>.
/// <para>
/// It runs as an action filter, so the eviction happens after the action committed its transaction
/// and before the response is written: a client that reads right after the write never sees stale data.
/// Failed writes (exceptions, validation short-circuits, 4xx results) evict nothing.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class InvalidatesCatalogCacheAttribute(CatalogCacheScope scope) : Attribute, IAsyncActionFilter
{
    /// <summary>Which cached responses become stale.</summary>
    public CatalogCacheScope Scope { get; } = scope;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return;
        }

        var failed = executed.Canceled
            || (executed.Exception is not null && !executed.ExceptionHandled)
            || (executed.Result is IStatusCodeActionResult { StatusCode: >= 400 });
        if (failed)
        {
            return;
        }

        var invalidator = context.HttpContext.RequestServices.GetRequiredService<CatalogCacheInvalidator>();
        await invalidator.EvictAsync(Scope, CancellationToken.None);
    }
}
