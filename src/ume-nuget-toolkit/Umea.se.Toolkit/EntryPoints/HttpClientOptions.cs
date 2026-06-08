using Umea.se.Toolkit.Auth.BearerToken;

namespace Umea.se.Toolkit.EntryPoints;

/// <summary>
/// Options used to configure HttpClient BaseAddress, Certificate, X-Api-Key, default headers
/// and optionally an Entra ID bearer token attached to every outgoing request.
/// </summary>
public class HttpClientOptions
{
    public string? BaseAddress { get; set; }
    public string? XApiKey { get; set; }
    public string? CertificateName { get; set; }
    public IDictionary<string, string> DefaultRequestHeaders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// When set, every request through this HttpClient gets an <c>Authorization: Bearer &lt;token&gt;</c>
    /// header. The token is acquired from Entra ID using the credential and endpoint version
    /// described by <see cref="TokenCredentials"/>.
    /// </summary>
    public TokenCredentials? BearerToken { get; set; }
}
