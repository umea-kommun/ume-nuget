using Microsoft.AspNetCore.Http;
using Umea.se.Toolkit.Logging;

namespace Umea.se.Toolkit.ExternalService;

/// <summary>
/// Forwards the App Insights user/session headers from the current inbound request
/// onto outgoing calls, so downstream services log the same user_Id / session_Id.
/// .NET HttpClient does not propagate inbound headers, so without this the ids stop
/// at the first service.
/// </summary>
internal sealed class UserContextForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private static readonly string[] HeadersToForward = [UserContextHeaderNames.UserId, UserContextHeaderNames.SessionId];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        IHeaderDictionary? incomingHeaders = httpContextAccessor.HttpContext?.Request.Headers;
        if (incomingHeaders is not null)
        {
            foreach (string header in HeadersToForward)
            {
                if (request.Headers.Contains(header))
                {
                    continue;
                }

                string? value = incomingHeaders[header].FirstOrDefault();
                if (!string.IsNullOrEmpty(value))
                {
                    request.Headers.TryAddWithoutValidation(header, value);
                }
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
