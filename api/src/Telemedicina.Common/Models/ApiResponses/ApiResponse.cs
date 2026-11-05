namespace Telemedicina.Common.Models.ApiResponses;

public class ApiResponse<T> : BaseApiResponse
{
    public T? Data { get; init; }

    public ApiResponse(int status) : base(status) { }

    public ApiResponse(string detail) : base(status: 200, detail: detail) { }

    public ApiResponse(T data) : base(status: 200) => Data = data;

    public ApiResponse(int status, string detail) : base(status, detail) { }

    public ApiResponse(int status, T data) : base(status) => Data = data;

    public ApiResponse(string detail, T data) : base(200, detail) => Data = data;

    public ApiResponse(int status, string detail, T data) : base(status, detail) => Data = data;
}
