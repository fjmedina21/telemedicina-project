namespace MyCompany.Models.ApiResponses;

public class ErrorApiResponse : BaseApiResponse
{
    public string Type { get; init; }
    public string Title { get; init; }
    public string Instance { get; init; }
    public IReadOnlyList<string> Errors { get; init; }

    public ErrorApiResponse(
        int status,
        string title,
        string instance,
        IEnumerable<string>? errors)
        : base(status)
    {
        Title = title;
        Instance = instance;
        Type = DefaultType(status);
        Errors = errors?.ToArray() ?? [];
    }

    public ErrorApiResponse(
        int status,
        string? type,
        string title,
        string? detail,
        string instance,
        IEnumerable<string>? errors)
        : base(status, detail)
    {
        Title = title;
        Instance = instance;
        Type = type ?? DefaultType(status);
        Errors = errors?.ToArray() ?? [];
    }

    private static string DefaultType(int status) => status switch
    {
        400 or 401 or 402 or 403 or 404 or 500 or 503 or 504 =>
            $"https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/{status}",
        _ => "https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status"
    };
}
