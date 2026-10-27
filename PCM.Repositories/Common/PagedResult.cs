using Microsoft.EntityFrameworkCore;

namespace PCM.Repositories.Common;

/// <summary>One page of a larger result set.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class PagingExtensions
{
    /// <summary>
    /// Runs a COUNT and a paged SELECT on an ordered query.
    /// The query must already be ordered, otherwise SQL Server paging is not deterministic.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IOrderedQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }
}
