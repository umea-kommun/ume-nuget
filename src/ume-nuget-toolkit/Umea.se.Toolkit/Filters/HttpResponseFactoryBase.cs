using System.Net;

namespace Umea.se.Toolkit.Filters;

/// <summary>
/// These special exceptions are meant to be caught by the web framework.
/// They are turned into web-responses by HttpResponseExceptionFilter.
/// They may ONLY be thrown in controllers endpoint methods.
/// To provide custom exceptions/error-responses, inherit from this class and write similar static methods
/// </summary>
public class HttpResponseFactoryBase
{
    public static HttpResponseException Created(string? message = null)
        => CreateCreatedResponse(new ResponseBody(message));

    public static HttpResponseException BadRequest(string? message = null)
        => CreateBadRequestResponse(new ResponseBody(message));

    public static HttpResponseException Unauthorized(string? message = null)
        => CreateUnauthorizedResponse(new ResponseBody(message));

    public static HttpResponseException Unauthorized_ApiKeyMissing()
        => CreateUnauthorizedResponse(new ResponseBody("X-Api-Key is missing"));

    public static HttpResponseException Forbidden(string? message = null)
        => CreateForbiddenResponse(new ResponseBody(message));

    public static HttpResponseException Forbidden_ApiKeyInvalid()
        => CreateForbiddenResponse(new ResponseBody("X-Api-Key is invalid"));

    public static HttpResponseException NotFound(string? message = null)
        => CreateNotFoundResponse(new ResponseBody(message));

    public static HttpResponseException NotFound<T>()
        => CreateNotFoundResponse(new ResponseBody($"{typeof(T).Name} was not found"));

    public static HttpResponseException Conflict(string? message = null)
        => CreateConflictResponse(new ResponseBody(message));

    public static HttpResponseException Gone(string? message = null)
        => CreateGoneResponse(new ResponseBody(message));

    public static HttpResponseException InternalServerError(string? message = null)
        => CreateInternalServerErrorResponse(new ResponseBody(message));

    public static HttpResponseException InternalServerError_WithDetails(Exception exception)
        => CreateInternalServerErrorResponse(new ResponseBody(exception.Message, exception.StackTrace?.Trim()));

    private static HttpResponseException CreateCreatedResponse(ResponseBody? value = null)
        => new(HttpStatusCode.Created, value);

    private static HttpResponseException CreateBadRequestResponse(ResponseBody? value = null)
        => new(HttpStatusCode.BadRequest, value);

    private static HttpResponseException CreateUnauthorizedResponse(ResponseBody? value = null)
        => new(HttpStatusCode.Unauthorized, value);

    private static HttpResponseException CreateForbiddenResponse(ResponseBody? value = null)
        => new(HttpStatusCode.Forbidden, value);

    private static HttpResponseException CreateNotFoundResponse(ResponseBody? value = null)
        => new(HttpStatusCode.NotFound, value);

    private static HttpResponseException CreateConflictResponse(ResponseBody? value = null)
        => new(HttpStatusCode.Conflict, value);

    private static HttpResponseException CreateGoneResponse(ResponseBody? value = null)
        => new(HttpStatusCode.Gone, value);

    public static HttpResponseException CreateInternalServerErrorResponse(ResponseBody? value = null)
        => new(HttpStatusCode.InternalServerError, value);

}
