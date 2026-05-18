using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Umea.se.Toolkit.HealthChecks;

/// <summary>
/// Configures how the overall <see cref="HealthStatus"/> is derived from individual check results.
/// </summary>
public sealed class HealthCheckSetupOptions
{
    /// <summary>
    /// Number of simultaneously unhealthy checks required to report <see cref="HealthStatus.Unhealthy"/>.
    /// Does not apply to checks listed in <see cref="CriticalChecks"/>. Defaults to 3.
    /// </summary>
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>
    /// Number of simultaneously unhealthy checks required to report <see cref="HealthStatus.Degraded"/>.
    /// Must be less than <see cref="UnhealthyThreshold"/>. Defaults to 1.
    /// </summary>
    public int DegradedThreshold { get; set; } = 1;

    /// <summary>
    /// Health check names that are considered critical. If any of these report
    /// <see cref="HealthStatus.Unhealthy"/>, the overall status is immediately
    /// <see cref="HealthStatus.Unhealthy"/> regardless of <see cref="UnhealthyThreshold"/>.
    /// Names are matched case-insensitively.
    /// </summary>
    public ISet<string> CriticalChecks { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public static class HealthCheckSetup
{
    /// <summary>
    /// Maps the three standard health check endpoints:
    /// <list type="bullet">
    ///   <item><c>/api/v1.0/health</c> — full JSON report for dashboards and operators.</item>
    ///   <item><c>/api/v1.0/health/ready</c> — all checks must pass before traffic is routed here.</item>
    ///   <item><c>/api/v1.0/health/live</c> — liveness probe; confirms the process is alive with no dependency calls.</item>
    /// </list>
    /// <para>
    /// The overall status is derived from the individual check results using the following priority order:
    /// <list type="number">
    ///   <item>If any check listed in <see cref="HealthCheckSetupOptions.CriticalChecks"/> is <see cref="HealthStatus.Unhealthy"/>, the overall status is immediately <see cref="HealthStatus.Unhealthy"/>.</item>
    ///   <item>If the number of unhealthy checks meets or exceeds <see cref="HealthCheckSetupOptions.UnhealthyThreshold"/>, the overall status is <see cref="HealthStatus.Unhealthy"/>.</item>
    ///   <item>If the number of unhealthy checks meets or exceeds <see cref="HealthCheckSetupOptions.DegradedThreshold"/>, the overall status is <see cref="HealthStatus.Degraded"/>.</item>
    ///   <item>If any check reports <see cref="HealthStatus.Degraded"/>, the overall status is <see cref="HealthStatus.Degraded"/>.</item>
    ///   <item>Otherwise the overall status is <see cref="HealthStatus.Healthy"/>.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="app">The application to configure.</param>
    /// <param name="configure">
    /// Optional delegate to configure <see cref="HealthCheckSetupOptions"/>.
    /// Use this to set <see cref="HealthCheckSetupOptions.UnhealthyThreshold"/>,
    /// <see cref="HealthCheckSetupOptions.DegradedThreshold"/>, and
    /// <see cref="HealthCheckSetupOptions.CriticalChecks"/> per application.
    /// </param>
    public static WebApplication UseDefaultHealthChecks(this WebApplication app, Action<HealthCheckSetupOptions>? configure = null)
    {
        HealthCheckSetupOptions options = new();
        configure?.Invoke(options);

        HealthCheckOptions healthCheckOptions = new()
        {
            ResponseWriter = (context, report) =>
            {
                int unhealthyCount = report.Entries.Values.Count(e => e.Status == HealthStatus.Unhealthy);
                bool criticalFailed = options.CriticalChecks.Count > 0 &&
                    report.Entries.Any(e => options.CriticalChecks.Contains(e.Key) && e.Value.Status == HealthStatus.Unhealthy);

                HealthStatus overallStatus = true switch
                {
                    _ when criticalFailed => HealthStatus.Unhealthy,
                    _ when unhealthyCount >= options.UnhealthyThreshold => HealthStatus.Unhealthy,
                    _ when unhealthyCount >= options.DegradedThreshold => HealthStatus.Degraded,
                    _ when report.Entries.Values.Any(e => e.Status == HealthStatus.Degraded) => HealthStatus.Degraded,
                    _ => HealthStatus.Healthy
                };

                HealthReport adjustedReport = new(report.Entries, overallStatus, report.TotalDuration);
                return UIResponseWriter.WriteHealthCheckUIResponse(context, adjustedReport);
            }
        };

        app.MapHealthChecks("/api/v1.0/health", healthCheckOptions);
        app.MapHealthChecks("/api/v1.0/health/ready", healthCheckOptions);
        app.MapHealthChecks("/api/v1.0/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        return app;
    }
}
