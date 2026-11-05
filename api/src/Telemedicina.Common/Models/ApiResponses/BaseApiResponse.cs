namespace Telemedicina.Common.Models.ApiResponses;

public abstract class BaseApiResponse(
    int status = 200,
    string? detail = null)
{
    public bool Success { get; init; } = status >= 200 && status < 400;
    public int Status { get; init; } = status;
    public string Detail { get; init; } = detail ?? DefaultMessage(status);

    protected static string DefaultMessage(int status) => status switch
    {
        200 => "The request was successful.",
        201 => "Resource created.",
        202 => "The request has been accepted for processing, but not completed.",
        204 => "The server successfully processed the request and is not returning any content.",
        400 => "The server could not understand the request due to invalid syntax.",
        401 => "Authentication is required to access this resource.",
        402 => "Reserved for future use; used in digital payment systems.",
        403 => "The client does not have access rights to the content.",
        404 => "The server cannot find the requested resource.",
        409 => "The request could not be processed due to a conflict in the current state of the resource.",
        500 => "The server was unable to complete your request. Please try again later.",
        503 => "The server is currently unavailable (overloaded or down). Please try again later.",
        504 => "The server did not receive a timely response from an upstream server.",
        _ => "HTTP Status Code not defined"
    };
}
