using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Umea.se.Toolkit.Logging;

public sealed class UserContextLogRecordProcessor(IHttpContextAccessor httpContextAccessor) : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        IHeaderDictionary? headers = httpContextAccessor.HttpContext?.Request.Headers;
        if (headers is null)
        {
            return;
        }

        List<KeyValuePair<string, object?>> attributes = [];

        string? userId = headers[UserContextHeaderNames.UserId].FirstOrDefault();
        if (!string.IsNullOrEmpty(userId))
        {
            attributes.Add(new KeyValuePair<string, object?>("enduser.pseudo.id", userId));
        }

        string? sessionId = headers[UserContextHeaderNames.SessionId].FirstOrDefault();
        if (!string.IsNullOrEmpty(sessionId))
        {
            attributes.Add(new KeyValuePair<string, object?>("microsoft.session.id", sessionId));
        }

        if (attributes.Count > 0)
        {
            data.Attributes = [.. (data.Attributes ?? []), .. attributes];
        }
    }
}
