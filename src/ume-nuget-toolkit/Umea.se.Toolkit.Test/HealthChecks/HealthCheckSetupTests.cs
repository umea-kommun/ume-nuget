using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Umea.se.Toolkit.HealthChecks;

namespace Umea.se.Toolkit.Test.HealthChecks;

public sealed class HealthCheckSetupTests
{
    private const string HealthPath = "/api/v1.0/health";
    private const string ReadyPath = "/api/v1.0/health/ready";
    private const string LivePath = "/api/v1.0/health/live";

    private static async Task<(HttpStatusCode StatusCode, string Body)> GetAsync(
        Action<IHealthChecksBuilder> configure,
        string path = HealthPath,
        Action<HealthCheckSetupOptions>? configureOptions = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        IHealthChecksBuilder healthChecksBuilder = builder.Services.AddHealthChecks();
        configure(healthChecksBuilder);

        WebApplication app = builder.Build();
        app.UseDefaultHealthChecks(configureOptions);

        await app.StartAsync();
        try
        {
            HttpClient client = app.GetTestClient();
            HttpResponseMessage response = await client.GetAsync(path);
            string body = await response.Content.ReadAsStringAsync();
            return (response.StatusCode, body);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static string ParseStatus(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("status").GetString()!;

    // ── /health endpoint ──────────────────────────────────────────────────────

    [Fact]
    public async Task Health_AllHealthy_ReturnsHealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(b =>
        {
            b.AddCheck("check1", () => HealthCheckResult.Healthy());
            b.AddCheck("check2", () => HealthCheckResult.Healthy());
        });

        statusCode.ShouldBe(HttpStatusCode.OK);
        ParseStatus(body).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Health_OneDegradedNoUnhealthy_ReturnsDegraded()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(b =>
        {
            b.AddCheck("check1", () => HealthCheckResult.Healthy());
            b.AddCheck("check2", () => HealthCheckResult.Degraded());
        });

        statusCode.ShouldBe(HttpStatusCode.OK);
        ParseStatus(body).ShouldBe("Degraded");
    }

    [Fact]
    public async Task Health_OneUnhealthy_BelowThreshold_ReturnsDegraded()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(b =>
        {
            b.AddCheck("check1", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check2", () => HealthCheckResult.Healthy());
        });

        ParseStatus(body).ShouldBe("Degraded");
    }

    [Fact]
    public async Task Health_TwoUnhealthy_BelowDefaultThreshold_ReturnsDegraded()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(b =>
        {
            b.AddCheck("check1", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check2", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check3", () => HealthCheckResult.Healthy());
        });

        ParseStatus(body).ShouldBe("Degraded");
    }

    [Fact]
    public async Task Health_ThreeUnhealthy_AtDefaultThreshold_ReturnsUnhealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(b =>
        {
            b.AddCheck("check1", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check2", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check3", () => HealthCheckResult.Unhealthy());
        });

        ParseStatus(body).ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task Health_FourUnhealthy_AboveThreshold_ReturnsUnhealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(b =>
        {
            b.AddCheck("check1", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check2", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check3", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check4", () => HealthCheckResult.Unhealthy());
        });

        ParseStatus(body).ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task Health_CustomThresholdOfOne_OneUnhealthy_ReturnsUnhealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(
            b => b.AddCheck("check1", () => HealthCheckResult.Unhealthy()),
            configureOptions: o => o.UnhealthyThreshold = 1);

        ParseStatus(body).ShouldBe("Unhealthy");
    }

    [Fact]
    public async Task Health_CustomThresholdOfTwo_OneUnhealthy_ReturnsDegraded()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(b =>
        {
            b.AddCheck("check1", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check2", () => HealthCheckResult.Healthy());
        },
        configureOptions: o => o.UnhealthyThreshold = 2);

        ParseStatus(body).ShouldBe("Degraded");
    }

    [Fact]
    public async Task Health_NoChecksRegistered_ReturnsHealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(_ => { });

        statusCode.ShouldBe(HttpStatusCode.OK);
        ParseStatus(body).ShouldBe("Healthy");
    }

    // ── /health/ready endpoint ────────────────────────────────────────────────

    [Fact]
    public async Task Ready_AllHealthy_ReturnsHealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(
            b => b.AddCheck("check1", () => HealthCheckResult.Healthy()),
            path: ReadyPath);

        statusCode.ShouldBe(HttpStatusCode.OK);
        ParseStatus(body).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Ready_ThreeUnhealthy_AtThreshold_ReturnsUnhealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(b =>
        {
            b.AddCheck("check1", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check2", () => HealthCheckResult.Unhealthy());
            b.AddCheck("check3", () => HealthCheckResult.Unhealthy());
        },
        path: ReadyPath);

        ParseStatus(body).ShouldBe("Unhealthy");
    }

    // ── /health/live endpoint ─────────────────────────────────────────────────

    [Fact]
    public async Task Live_AlwaysReturnsHealthy_EvenWhenChecksAreUnhealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(
            b => b.AddCheck("check1", () => HealthCheckResult.Unhealthy()),
            path: LivePath);

        statusCode.ShouldBe(HttpStatusCode.OK);
        ParseStatus(body).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Live_NoChecksEvaluated_ReturnsHealthy()
    {
        (HttpStatusCode statusCode, string? body) = await GetAsync(
            b => b.AddCheck("check1", () => HealthCheckResult.Degraded()),
            path: LivePath);

        statusCode.ShouldBe(HttpStatusCode.OK);
        ParseStatus(body).ShouldBe("Healthy");
    }

    // ── endpoint mapping ──────────────────────────────────────────────────────

    [Fact]
    public async Task UseDefaultHealthChecks_ReturnsApp_ForFluentChaining()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHealthChecks();

        WebApplication app = builder.Build();
        WebApplication result = app.UseDefaultHealthChecks();

        result.ShouldBeSameAs(app);

        await app.StartAsync();
        await app.StopAsync();
    }
}
