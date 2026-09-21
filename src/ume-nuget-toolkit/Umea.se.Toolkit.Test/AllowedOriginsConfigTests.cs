using Shouldly;
using Umea.se.Toolkit.Test.Infrastructure;

namespace Umea.se.Toolkit.Test;

public sealed class AllowedOriginsConfigTests
{
    private const string BaseSettings = """{ "Cors": { "AllowedOrigins": [ "http://localhost:8080", "https://a.dev.umea.se", "https://b.dev.umea.se" ] } }""";

    [Fact]
    public void EnvironmentSettings_ReplaceBaseArray_InsteadOfMergingPerIndex()
    {
        TestApplicationConfig config = TestApplicationConfigBuilder.CreateBuilder()
            .WithJson(BaseSettings)
            .WithJson("""{ "Cors": { "AllowedOrigins": [ "https://prod.umea.se" ] } }""")
            .BuildConfig();

        config.AllowedOrigins.ShouldBe(["https://prod.umea.se"]);
    }

    [Fact]
    public void EmptyArrayInEnvironmentSettings_ClearsBaseArray()
    {
        TestApplicationConfig config = TestApplicationConfigBuilder.CreateBuilder()
            .WithJson(BaseSettings)
            .WithJson("""{ "Cors": { "AllowedOrigins": [] } }""")
            .BuildConfig();

        config.AllowedOrigins.ShouldBeEmpty();
    }

    [Fact]
    public void MissingInEnvironmentSettings_FallsBackToBaseArray()
    {
        TestApplicationConfig config = TestApplicationConfigBuilder.CreateBuilder()
            .WithJson(BaseSettings)
            .WithJson("""{ "Logging": { "LogLevel": { "Default": "Warning" } } }""")
            .BuildConfig();

        config.AllowedOrigins.ShouldBe(["http://localhost:8080", "https://a.dev.umea.se", "https://b.dev.umea.se"]);
    }

    [Fact]
    public void LaterSource_OverridesFileArray()
    {
        TestApplicationConfig config = TestApplicationConfigBuilder.CreateBuilder()
            .WithJson(BaseSettings)
            .WithConfiguration("Cors:AllowedOrigins:0", "https://override.umea.se")
            .BuildConfig();

        config.AllowedOrigins.ShouldBe(["https://override.umea.se"]);
    }

    [Fact]
    public void ArrayOrder_IsNumeric()
    {
        string origins = string.Join(", ", Enumerable.Range(0, 12).Select(i => $"\"https://{i}.umea.se\""));
        TestApplicationConfig config = TestApplicationConfigBuilder.CreateBuilder()
            .WithJson($$"""{ "Cors": { "AllowedOrigins": [ {{origins}} ] } }""")
            .BuildConfig();

        config.AllowedOrigins.ShouldBe([.. Enumerable.Range(0, 12).Select(i => $"https://{i}.umea.se")]);
    }

    [Fact]
    public void NotConfigured_ReturnsEmpty()
    {
        TestApplicationConfig config = TestApplicationConfigBuilder.CreateBuilder().BuildConfig();

        config.AllowedOrigins.ShouldBeEmpty();
    }
}
