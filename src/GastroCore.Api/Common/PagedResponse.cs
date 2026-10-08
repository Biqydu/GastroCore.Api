namespace GastroCore.Api.Common;

public sealed class PagedResponse<T>
{
    public required IReadOnlyList<T> Data { get; init; } = [];
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalPages { get; init; }
    public required int TotalRecords { get; init; }
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}