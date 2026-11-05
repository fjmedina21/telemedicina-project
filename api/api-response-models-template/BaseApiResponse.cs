using Microsoft.AspNetCore.Http;

namespace MyCompany.Models.ApiResponses;

public abstract class BaseApiResponse(
    int status = StatusCodes.Status200OK, 
    string? detail = null)
{
    public bool Success { get; init; }= status;
    public int Status { get; init; } = status < StatusCodes.Status400BadRequest;
    public string Detail { get; init; }= detail ?? DefaultMessage(status);

    protected static string DefaultMessage(int status) => status switch
    {
        StatusCodes.Status200OK => "The request was successful.",
        StatusCodes.Status201Created => "Resource created.",
        StatusCodes.Status202Accepted => "The request has been accepted for processing, but not completed.",
        StatusCodes.Status204NoContent => "The server successfully processed the request and is not returning any content.",
        StatusCodes.Status400BadRequest => "The server could not understand the request due to invalid syntax.",
        StatusCodes.Status401Unauthorized => "Authentication is required to access this resource.",
        StatusCodes.Status402PaymentRequired => "Reserved for future use; used in digital payment systems.",
        StatusCodes.Status403Forbidden => "The client does not have access rights to the content.",
        StatusCodes.Status404NotFound => "The server cannot find the requested resource.",
        StatusCodes.Status500InternalServerError => "The server was unable to complete your request. Please try again later.",
        StatusCodes.Status503ServiceUnavailable => "The server is currently unavailable (overloaded or down). Please try again later.",
        StatusCodes.Status504GatewayTimeout => "The server did not receive a timely response from an upstream server.",
        _ => "HTTP Status Code not defined"
    };
}
