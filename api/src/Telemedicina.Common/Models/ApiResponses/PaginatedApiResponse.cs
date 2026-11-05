namespace Telemedicina.Common.Models.ApiResponses;

public class PaginatedApiResponse<T> : BaseApiResponse
{
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }
    public IReadOnlyList<T> Data { get; init; }

    public PaginatedApiResponse(PagedResult<T> pagedResult)
        : base(200)
    {
        CurrentPage = pagedResult.CurrentPage;
        TotalPages = pagedResult.TotalPages;
        PageSize = pagedResult.PageSize;
        TotalCount = pagedResult.TotalCount;
        HasPrevious = pagedResult.HasPrevious;
        HasNext = pagedResult.HasNext;
        Data = pagedResult.Items;
    }
}
