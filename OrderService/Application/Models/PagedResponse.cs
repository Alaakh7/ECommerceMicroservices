namespace OrderService.Application.Models;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, long TotalItems, int TotalPages, bool HasPreviousPage, bool HasNextPage)
{
    public static PagedResponse<T> Create(IReadOnlyList<T> items, int pageNumber, int pageSize, long totalItems)
    {
        var pages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        return new(items, pageNumber, pageSize, totalItems, pages, pageNumber > 1, pageNumber < pages);
    }
}

public sealed record OperationResult<T>(T Value, int StatusCode, bool IsIdempotent = false);
