using System.Text.Json.Serialization;

namespace Umea.se.Toolkit.Auth.BearerToken;

/// <summary>
/// Client-secret credentials a <see cref="BearerTokenHandler"/> uses to acquire a bearer token.
/// <paramref name="Resource"/> is the resource identifier (e.g. <c>api://my-app-id</c> or the
/// resource's full URL), normalised to a <c>/.default</c> scope when the token is requested.
/// </summary>
public sealed record TokenCredentials(
    string TenantId,
    string ClientId,
    [property: JsonIgnore] string ClientSecret,
    string Resource)
{
    public override string ToString() =>
        $"TokenCredentials {{ TenantId = {TenantId}, ClientId = {ClientId}, ClientSecret = ***, Resource = {Resource} }}";
}
