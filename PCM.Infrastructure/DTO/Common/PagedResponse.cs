namespace PCM.Infrastructure.DTO.Common;

/// <summary>Response shape of every paginated list endpoint.</summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
