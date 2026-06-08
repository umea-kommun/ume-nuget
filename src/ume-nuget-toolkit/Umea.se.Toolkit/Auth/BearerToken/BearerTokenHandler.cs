using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;

namespace Umea.se.Toolkit.Auth.BearerToken;

/// <summary>
/// Attaches a bearer token to every outgoing request on a named HttpClient.
/// The credential is reused so Azure.Identity can cache and refresh tokens internally.
/// </summary>
public sealed class BearerTokenHandler(TokenCredential credential, string[] scopes) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext(scopes),
            cancellationToken).ConfigureAwait(false);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ClientSecretTokenCredentialSource(TokenCredentials credentials)
{
    private readonly Lazy<TokenCredential> _credential = new(() => new ClientSecretCredential(credentials.TenantId, credentials.ClientId, credentials.ClientSecret));

    public TokenCredential Credential => _credential.Value;

    public string[] Scopes { get; } = [ToDefaultScope(credentials.Resource)];

    private static string ToDefaultScope(string resource)
    {
        string normalizedResource = resource.TrimEnd('/');
        return normalizedResource.EndsWith("/.default", StringComparison.OrdinalIgnoreCase)
            ? normalizedResource
            : $"{normalizedResource}/.default";
    }
}
