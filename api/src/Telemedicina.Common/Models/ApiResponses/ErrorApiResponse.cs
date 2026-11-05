namespace Telemedicina.Common.Models.ApiResponses;

public class ErrorApiResponse : BaseApiResponse
{
    // Stable application metadata, shared by controllers and framework errors.
    public string? Code { get; init; }
    public string? TraceId { get; init; }
    public string Type { get; init; }
    public string Title { get; init; }
    public string Instance { get; init; }
    public IReadOnlyList<string> Errors { get; init; }

    public ErrorApiResponse(
        int status,
        string title,
        string instance,
        IEnumerable<string>? errors = null)
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
        IEnumerable<string>? errors = null)
        : base(status, detail)
    {
        Title = title;
        Instance = instance;
        Type = type ?? DefaultType(status);
        Errors = errors?.ToArray() ?? [];
    }

    private static string DefaultType(int status) => status switch
    {
        400 or 401 or 402 or 403 or 404 or 409 or 500 or 503 or 504 =>
            $"https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/{status}",
        _ => "https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status"
    };
}
