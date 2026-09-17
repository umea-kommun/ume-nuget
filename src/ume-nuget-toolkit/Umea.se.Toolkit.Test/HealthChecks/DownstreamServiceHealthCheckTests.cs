using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Umea.se.Toolkit.HealthChecks;

namespace Umea.se.Toolkit.Test.HealthChecks;

public sealed class DownstreamServiceHealthCheckTests
{
    private const string BaseAddress = "https://downstream.example/prefix/api/v1.0/thing/";

    // One closed generic type per test: suppression and result cache are static per T.
    private sealed class FallbackCheck(IHttpClientFactory factory)
        : TestDownstreamCheck<FallbackCheck>(factory);

    private sealed class SuppressionCheck(IHttpClientFactory factory)
        : TestDownstreamCheck<SuppressionCheck>(factory);

    private sealed class ModernDownstreamCheck(IHttpClientFactory factory)
        : TestDownstreamCheck<ModernDownstreamCheck>(factory);

    private sealed class FailingPingCheck(IHttpClientFactory factory)
        : TestDownstreamCheck<FailingPingCheck>(factory);

    private abstract class TestDownstreamCheck<T>(IHttpClientFactory factory)
        : DownstreamServiceHealthCheck<T>(factory, NullLogger<T>.Instance)
        where T : TestDownstreamCheck<T>
    {
        protected override string HttpClientName => "test-client";
        protected override string PingFallbackUrl => "ping";

        // Base class caches Healthy for 5 min, hiding the second request.
        protected override bool EnableCaching => false;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<string> RequestedPaths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedPaths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri(BaseAddress) };
    }

    private static RecordingHandler HandlerReturning(Func<string, HttpResponseMessage> byPath) =>
        new(request => byPath(request.RequestUri!.AbsolutePath));

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task HealthEndpoint404_FallsBackToPing_AndReportsHealthy()
    {
        RecordingHandler handler = HandlerReturning(path => path.EndsWith("/health")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK));

        FallbackCheck check = new(new StubHttpClientFactory(handler));

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
        handler.RequestedPaths.ShouldBe(["/prefix/api/v1.0/health", "/prefix/api/v1.0/thing/ping"]);
    }

    [Fact]
    public async Task HealthEndpoint404_SuppressesTheProbeOnSubsequentChecks()
    {
        RecordingHandler handler = HandlerReturning(path => path.EndsWith("/health")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK));

        SuppressionCheck check = new(new StubHttpClientFactory(handler));

        await check.CheckHealthAsync(new HealthCheckContext());
        handler.RequestedPaths.Clear();

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
        handler.RequestedPaths.ShouldBe(["/prefix/api/v1.0/thing/ping"]);
    }

    [Fact]
    public async Task HealthEndpointAvailable_KeepsProbingAndNeverPings()
    {
        RecordingHandler handler = HandlerReturning(_ => Json(HttpStatusCode.OK, """{"status":"Healthy"}"""));

        ModernDownstreamCheck check = new(new StubHttpClientFactory(handler));

        await check.CheckHealthAsync(new HealthCheckContext());
        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Healthy);
        handler.RequestedPaths.ShouldBe(["/prefix/api/v1.0/health", "/prefix/api/v1.0/health"]);
    }

    [Fact]
    public async Task PingFailure_AfterProbeSuppression_StillReportsUnhealthy()
    {
        RecordingHandler handler = HandlerReturning(path => path.EndsWith("/health")
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.BadGateway));

        FailingPingCheck check = new(new StubHttpClientFactory(handler));

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.ShouldBe(HealthStatus.Unhealthy);

        // Probe stays suppressed, so retries go straight to the ping.
        handler.RequestedPaths.Clear();
        await check.CheckHealthAsync(new HealthCheckContext());
        handler.RequestedPaths.ShouldAllBe(path => path == "/prefix/api/v1.0/thing/ping");
    }
}
