using Microsoft.AspNetCore.Http;
using MyCompany.Utils; // Ajustar únicamente al namespace real de tu librería Utils.

namespace MyCompany.Models.ApiResponses;

public class PaginatedApiResponse<T> : BaseApiResponse
{
    public int CurrentPage { get; init; }
    public int TotalPages { get; init; }
    public int PageCount { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get;} init;
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }
    public PagedList<T> Data { get; init; }

    public PaginatedApiResponse(PagedList<T> data)
        : base(StatusCodes.Status200OK)
    {
        CurrentPage = data.CurrentPage;
        TotalPages = data.TotalPages;
        PageCount = data.PageCount;
        PageSize = data.PageSize;
        TotalCount = data.TotalCount;
        HasPrevious = data.HasPrevious;
        HasNext = data.HasNext;
        Data = data;
    }
}
