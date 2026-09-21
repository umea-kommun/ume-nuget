using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Umea.se.Toolkit.Auth;
using Umea.se.Toolkit.Auth.Exceptions;
using Umea.se.Toolkit.Configuration.Exceptions;

namespace Umea.se.Toolkit.Configuration;

/// <summary>
/// Base ApplicationConfig for APIs. Simplifies configuration retrieval from appsettings.json.
/// </summary>
public abstract partial class ApplicationConfigBase
{
    public virtual string Environment => GetValue("ASPNETCORE_ENVIRONMENT");
    public abstract bool IsEnvironmentSafe { get; }

    public string ApiName => GetValue("Api:Name");
    public string ApiTitle => GetValue("Api:Title");
    public string ApiNameWithEnvironment => $"{ApiName} [{Environment}]";
    public string ApiTitleWithEnvironment => $"{ApiTitle} [{Environment}]";
    public string ApiDescription => GetValue("Api:Description");
    public string ApiVersion => GetValue("Api:Version");
    public Dictionary<string, string> ApiKeys => GetApiKeys();

    public string[] AllowedOrigins => GetOverridingArray("Cors:AllowedOrigins");

    public LogLevel LogLevel => TryGetEnum<LogLevel>("Logging:LogLevel:Umea") ?? GetEnum<LogLevel>("Logging:LogLevel:Default");
}

// ApplicationConfigBase functionality
public abstract partial class ApplicationConfigBase
{
    internal readonly IConfiguration Configuration;

    protected ApplicationConfigBase(IConfiguration configuration, Assembly? entryAssembly = null)
    {
        Configuration = configuration;
        this.ValidateApiKeys(entryAssembly);
    }

    protected string GetValue(string key)
    {
        return Configuration[key] ?? throw new ConfigurationNotFoundException(key);
    }

    protected T GetValue<T>(string key) where T : class
    {
        return Configuration
            .GetSection(key)
            .Get<T>() ?? throw new InvalidCastException($"Could not cast configuration [{key}] to type {typeof(T).Name}!");
    }

    protected string[] GetArray(string key)
    {
        return [.. Configuration.GetSection(key)
            .GetChildren()
            .Where(c => c.Value != null)
            .Select(c => c.Value!)];
    }

    /// <summary>
    /// Like <see cref="GetArray(string)"/>, but the array is taken whole from the last configuration source that defines it
    /// (e.g. appsettings.{env}.json over appsettings.json) instead of being merged per index across sources.
    /// An empty array (<c>[]</c>) in a later source clears the array.
    /// </summary>
    protected string[] GetOverridingArray(string key)
    {
        if (Configuration is not IConfigurationRoot root)
        {
            return GetArray(key);
        }

        foreach (IConfigurationProvider provider in root.Providers.Reverse())
        {
            string[] childKeys = [.. provider.GetChildKeys([], key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(ConfigurationKeyComparer.Instance)];

            bool definesEmptyArray = provider.TryGet(key, out string? value) && string.IsNullOrEmpty(value);
            if (childKeys.Length == 0 && !definesEmptyArray)
            {
                continue;
            }

            return [.. childKeys
                .Select(childKey => provider.TryGet(ConfigurationPath.Combine(key, childKey), out string? childValue) ? childValue : null)
                .OfType<string>()];
        }

        return [];
    }

    protected T[] GetArray<T>(string key) where T : class, new()
    {
        return [.. Configuration.GetSection(key)
            .GetChildren()
            .Select(section =>
            {
                T instance = new();
                section.Bind(instance);
                return instance;
            })];
    }

    protected int GetInt(string key)
    {
        return int.Parse(GetValue(key), CultureInfo.InvariantCulture);
    }

    protected float GetFloat(string key)
    {
        return float.Parse(GetValue(key), CultureInfo.InvariantCulture);
    }

    protected bool GetBool(string key)
    {
        return bool.Parse(GetValue(key));
    }

    protected T GetEnum<T>(string key) where T : struct
    {
        return Enum.Parse<T>(GetValue(key));
    }

    protected uint GetUint(string key)
    {
        return uint.Parse(GetValue(key), CultureInfo.InvariantCulture);
    }

    protected T? TryGetValue<T>(string key) where T : class
    {
        try
        {
            return GetValue<T>(key);
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    protected bool? TryGetBool(string key)
    {
        try
        {
            return GetBool(key);
        }
        catch (ConfigurationNotFoundException)
        {
            return null;
        }
    }

    protected T? TryGetEnum<T>(string key) where T : struct
    {
        try
        {
            return GetEnum<T>(key);
        }
        catch (ConfigurationNotFoundException)
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves API keys from configuration with backwards compatibility for legacy format.
    /// Supports both "Api:Key" (legacy single key) and "Api:Keys" (new dictionary format).
    /// Legacy "Api:Key" is automatically migrated to "Api:Keys:Default" with a deprecation warning.
    /// </summary>
    /// <returns>Dictionary of API key names to values</returns>
    /// <exception cref="ApiKeyValidationException">Thrown when both Api:Key and Api:Keys are configured</exception>
    private Dictionary<string, string> GetApiKeys()
    {
        Dictionary<string, string>? newFormatKeys = TryGetValue<Dictionary<string, string>>("Api:Keys");
        string? legacyKey = TryGetString("Api:Key");
        bool legacyKeyExists = Configuration["Api:Key"] is not null;
        bool newFormatExists = Configuration.GetSection("Api:Keys").Exists();

        // Error: Both formats configured - force user to complete migration
        if (newFormatExists && legacyKeyExists)
        {
            throw new ApiKeyValidationException(
                "Configuration conflict: both 'Api:Key' and 'Api:Keys' are configured. " +
                "Please complete the migration to 'Api:Keys' format by removing 'Api:Key'. " +
                "If using 'Api:Key' as the default key, move its value to 'Api:Keys:Default'.");
        }

        // New format only - use as-is
        if (newFormatKeys?.Count > 0)
        {
            return newFormatKeys;
        }

        // Legacy format only - migrate to Default key with deprecation warning
        if (!string.IsNullOrEmpty(legacyKey))
        {
            Console.WriteLine(
                "warn: Umea.se.Toolkit - Configuration 'Api:Key' is deprecated and will be removed in a future version. " +
                "Please migrate to 'Api:Keys:Default'.");

            return new Dictionary<string, string> { { "Default", legacyKey } };
        }

        // Neither format configured
        return [];
    }

    /// <summary>
    /// Attempts to retrieve a string value from configuration, returning null if not found.
    /// </summary>
    private string? TryGetString(string key)
    {
        try
        {
            return GetValue(key);
        }
        catch (ConfigurationNotFoundException)
        {
            return null;
        }
    }
}
