using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Umea.se.Toolkit.Test.Infrastructure;

public static class TestApplicationConfigBuilder
{
    public static ConfigurationBuilder CreateBuilder()
    {
        return new ConfigurationBuilder().WithEnvironment("unittests");
    }

    public static TestApplicationConfig BuildConfig(this ConfigurationBuilder builder, Assembly? entryAssembly = null)
    {
        IConfigurationRoot configuration = builder.Build();
        TestApplicationConfig config = new(configuration, entryAssembly);

        return config;
    }

    public static ConfigurationBuilder WithConfiguration(this ConfigurationBuilder builder, string key, string? value)
    {
        builder.AddInMemoryCollection(
        [
            new KeyValuePair<string, string?>(key, value),
        ]);

        return builder;
    }

    public static ConfigurationBuilder WithJson(this ConfigurationBuilder builder, string json)
    {
        builder.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        return builder;
    }

    public static ConfigurationBuilder WithEnvironment(this ConfigurationBuilder builder, string environment)
    {
        return builder.WithConfiguration("ASPNETCORE_ENVIRONMENT", environment);
    }

    public static ConfigurationBuilder WithApiKey(this ConfigurationBuilder builder, string key, int keyLength, char character = '0')
    {
        return builder.WithConfiguration($"Api:Keys:{key}", new string(character, keyLength));
    }

    public static ConfigurationBuilder WithApiKey(this ConfigurationBuilder builder, string key, string value)
    {
        return builder.WithConfiguration($"Api:Keys:{key}", value);
    }

    /// <summary>
    /// Configures the legacy Api:Key format (deprecated).
    /// Used for testing backwards compatibility.
    /// </summary>
    public static ConfigurationBuilder WithLegacyApiKey(this ConfigurationBuilder builder, string value)
    {
        return builder.WithConfiguration("Api:Key", value);
    }

    /// <summary>
    /// Configures the legacy Api:Key format with a generated value of specified length.
    /// Used for testing backwards compatibility.
    /// </summary>
    public static ConfigurationBuilder WithLegacyApiKey(this ConfigurationBuilder builder, int keyLength, char character = '0')
    {
        return builder.WithConfiguration("Api:Key", new string(character, keyLength));
    }
}
