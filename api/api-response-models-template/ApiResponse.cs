using Microsoft.AspNetCore.Http;

namespace MyCompany.Models.ApiResponses;

public class ApiResponse<T> : BaseApiResponse
{
    public T? Data { get; init; }

    public ApiResponse(int status) : base(status) { }

    public ApiResponse(string detail) : base(status: StatusCodes.Status200OK, detail: detail) { }

    public ApiResponse(T data) => Data = data;

    public ApiResponse(int status, string detail) : base(status, detail) { }

    public ApiResponse(int status, T data) : base(status) => Data = data;

    public ApiResponse(string detail, T data) : base(StatusCodes.Status200OK, detail) => Data = data;

    public ApiResponse(int status, string detail, T data) : base(status, detail) => Data = data;
}
