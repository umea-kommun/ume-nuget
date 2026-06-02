namespace Umea.se.Toolkit.Logging;

/// <summary>
/// Request headers carrying the App Insights session and anonymous user ids from
/// the frontend. They are read on the inbound side to stamp session_Id / user_Id
/// on telemetry, and forwarded on outbound calls so downstream services log the
/// same ids.
/// </summary>
internal static class UserContextHeaderNames
{
    internal const string UserId = "X-AI-User-Id";
    internal const string SessionId = "X-AI-Session-Id";
}
