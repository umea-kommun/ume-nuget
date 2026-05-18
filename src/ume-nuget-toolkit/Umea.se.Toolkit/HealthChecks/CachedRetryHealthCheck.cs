using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Umea.se.Toolkit.HealthChecks;

/// <summary>
/// Base class providing retry logic (up to 3 attempts with incremental backoff) and optional
/// 5-minute caching for health checks. The generic parameter T ensures each subclass gets its
/// own independent static cache. Override EnableCaching and return false to skip caching.
/// </summary>
public abstract class CachedRetryHealthCheck<T> : IHealthCheck where T : CachedRetryHealthCheck<T>
{
    private const int MaxRetries = 3;
    private static HealthCheckResult? _cachedResult;
    private static DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;
    private static readonly Lock _cacheLock = new();

    protected virtual bool EnableCaching => true;
    protected virtual TimeSpan CacheDuration => TimeSpan.FromMinutes(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (EnableCaching)
        {
            lock (_cacheLock)
            {
                if (_cachedResult.HasValue && DateTimeOffset.UtcNow < _cacheExpiry)
                {
                    return AsCachedResult(_cachedResult.Value);
                }
            }
        }

        HealthCheckResult result = await CallWithRetriesAsync(cancellationToken);

        if (EnableCaching && result.Status == HealthStatus.Healthy)
        {
            lock (_cacheLock)
            {
                if (!_cachedResult.HasValue || DateTimeOffset.UtcNow >= _cacheExpiry)
                {
                    SetCache(result, CacheDuration);
                }
            }
        }

        return result;
    }

    protected abstract Task<HealthCheckResult> ExecuteAsync(CancellationToken cancellationToken);

    protected virtual HealthCheckResult BuildUnhealthyResult(Exception exception, int attempts) =>
        HealthCheckResult.Unhealthy($"{exception.Message} (failed after {attempts} attempts)");

    protected virtual TimeSpan RetryDelay(int attempt) => TimeSpan.FromSeconds(attempt);

    private async Task<HealthCheckResult> CallWithRetriesAsync(CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await ExecuteAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;

                if (attempt < MaxRetries)
                {
                    await Task.Delay(RetryDelay(attempt), cancellationToken);
                }
            }
        }

        return BuildUnhealthyResult(lastException!, MaxRetries);
    }

    private static void SetCache(HealthCheckResult result, TimeSpan duration)
    {
        _cachedResult = result;
        _cacheExpiry = DateTimeOffset.UtcNow.Add(duration);
    }

    private static HealthCheckResult AsCachedResult(HealthCheckResult result) =>
        new(result.Status, $"[Cached] {result.Description}", result.Exception, result.Data);
}
