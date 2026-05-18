using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Umea.se.Toolkit.HealthChecks;

/// <summary>
/// Intermediate base class for health checks that probe a downstream service via
/// a standard "/api/v1.0/health" endpoint with an optional legacy ping fallback.
/// Subclasses only need to supply <see cref="HttpClientName"/> and <see cref="PingFallbackUrl"/>.
/// Retry and caching behaviour is inherited from <see cref="CachedRetryHealthCheck{T}"/>.
/// </summary>
public abstract class DownstreamServiceHealthCheck<T>(
    IHttpClientFactory httpClientFactory,
    ILogger<T> logger) : CachedRetryHealthCheck<T>
    where T : DownstreamServiceHealthCheck<T>
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<T> _logger = logger;

    protected abstract string HttpClientName { get; }
    protected abstract string PingFallbackUrl { get; }

    protected override async Task<HealthCheckResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

        // The configured BaseAddress may include service-specific path segments
        // (e.g. /api/v1.0/ecos/ or /api/v2.0/). The health endpoint is always
        // at /api/v1.0/health, so we derive it as an absolute URL by finding
        // the /api/ boundary in the BaseAddress.
        // The ping fallback uses the full BaseAddress as-is — it matches the same
        // path the service controllers use (e.g. BaseAddress + "ping").
        string healthUrl = BuildHealthUrl(client.BaseAddress);
        string pingUrl = BuildFullUrl(client.BaseAddress, PingFallbackUrl);

        HttpResponseMessage response = await client.GetAsync(healthUrl, cancellationToken);

        // Fall back to legacy ping if the health endpoint is not yet deployed or requires auth
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
        {
            response = await client.GetAsync(pingUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"{HttpClientName} returned HTTP {(int)response.StatusCode}",
                    null,
                    response.StatusCode);
            }

            return HealthCheckResult.Healthy($"{HttpClientName} ping responded successfully.");
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        HealthStatus? downstreamStatus = ParseHealthStatus(body);

        // Deliberate Unhealthy/Degraded from the health body means the service is knowingly in a bad
        // state — don't retry. For unrecognised responses with a non-success status, throw so the
        // base class retry fires (e.g. transient 503 from a gateway).
        return downstreamStatus switch
        {
            HealthStatus.Healthy => HealthCheckResult.Healthy($"{HttpClientName} responded successfully."),
            HealthStatus.Degraded => HealthCheckResult.Degraded($"{HttpClientName} is degraded"),
            HealthStatus.Unhealthy => HealthCheckResult.Unhealthy($"{HttpClientName} is unhealthy"),
            _ when !response.IsSuccessStatusCode => throw new HttpRequestException(
                $"{HttpClientName} returned HTTP {(int)response.StatusCode}",
                null,
                response.StatusCode),
            _ => HealthCheckResult.Healthy($"{HttpClientName} responded successfully.")
        };
    }

    /// <summary>
    /// Finds the /api/ segment in the BaseAddress and builds the absolute health URL:
    /// https://host/[prefix]/api/v1.0/health — regardless of the version or
    /// service-specific path suffix in the configured URL.
    /// </summary>
    private static string BuildHealthUrl(Uri? baseAddress)
    {
        if (baseAddress is null)
        {
            return string.Empty;
        }

        string url = baseAddress.ToString();
        int index = url.IndexOf("/api/", StringComparison.OrdinalIgnoreCase);
        string apiRoot = index >= 0 ? url[..(index + 5)] : url.TrimEnd('/') + "/";
        return $"{apiRoot}v1.0/health";
    }

    /// <summary>
    /// Appends <paramref name="path"/> directly to the full BaseAddress (trimmed to a
    /// trailing slash), matching the same behaviour as the service controllers.
    /// </summary>
    private static string BuildFullUrl(Uri? baseAddress, string path)
    {
        if (baseAddress is null)
        {
            return path;
        }

        return baseAddress.ToString().TrimEnd('/') + "/" + path;
    }

    private HealthStatus? ParseHealthStatus(string body)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("status", out JsonElement statusElement))
            {
                return statusElement.GetString() switch
                {
                    "Healthy" => HealthStatus.Healthy,
                    "Degraded" => HealthStatus.Degraded,
                    "Unhealthy" => HealthStatus.Unhealthy,
                    _ => null
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse health status from {HttpClientName} response body.", HttpClientName);
        }

        return null;
    }
}
