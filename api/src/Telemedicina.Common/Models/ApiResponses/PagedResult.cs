namespace Telemedicina.Common.Models.ApiResponses;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; }
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
    public bool HasPrevious => CurrentPage > 1;
    public bool HasNext => CurrentPage < TotalPages;

    public PagedResult(IEnumerable<T> items, int count, int pageNumber, int pageSize)
    {
        Items = items.ToList().AsReadOnly();
        TotalCount = count;
        CurrentPage = pageNumber;
        PageSize = pageSize;
    }
}
