namespace PCM.Infrastructure.DTO.Common;

/// <summary>
/// Query-string parameters shared by list endpoints: ?page=1&amp;pageSize=20&amp;sort=name.
/// Limits are enforced by the endpoint's validator.
/// </summary>
public class PageQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Items per page, 1 to 100.</summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Sort key; allowed values depend on the endpoint. A leading '-' means descending.</summary>
    public string? Sort { get; init; }
}
