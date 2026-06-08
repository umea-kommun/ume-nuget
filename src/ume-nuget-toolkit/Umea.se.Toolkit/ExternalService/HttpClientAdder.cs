using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Umea.se.Toolkit.Auth.BearerToken;
using Umea.se.Toolkit.EntryPoints;
using Umea.se.Toolkit.KeyVault;

namespace Umea.se.Toolkit.ExternalService;

internal static class HttpClientAdder
{
    internal static IServiceCollection Add(IServiceCollection services, string clientName, Action<HttpClientOptions>? configureOptions)
    {
        HttpClientOptions options = new();
        configureOptions?.Invoke(options);

        // Eager-load the client cert at registration time so we only write one
        // key file to the machine key store per process, instead of one per
        // HttpMessageHandler rotation. Skip when Key Vault isn't connected
        // (e.g. unit tests with SuppressKeyVaultConfigs = true) — tests mock
        // the HttpClient so the handler factory is never invoked anyway.
        X509Certificate2? clientCertificate = options.CertificateName is not null && KeyVaultService.IsConnected
            ? KeyVaultService.GetCertificate(options.CertificateName)
            : null;

        services.AddHttpContextAccessor();
        services.TryAddTransient<UserContextForwardingHandler>();

        IHttpClientBuilder builder = services
            .AddHttpClient(clientName)
            .ConfigureHttpClient(httpClient =>
            {
                if (options.BaseAddress is not null)
                {
                    httpClient.BaseAddress = new Uri(options.BaseAddress);
                }

                if (options.XApiKey is not null)
                {
                    httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
                    httpClient.DefaultRequestHeaders.Add("X-Api-Key", options.XApiKey);
                }

                if (options.DefaultRequestHeaders.Count > 0)
                {
                    foreach (KeyValuePair<string, string> header in options.DefaultRequestHeaders)
                    {
                        if (string.IsNullOrWhiteSpace(header.Key))
                        {
                            continue;
                        }

                        httpClient.DefaultRequestHeaders.Remove(header.Key);
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                HttpClientHandler handler = new();

                if (clientCertificate is not null)
                {
                    handler.SslProtocols = SslProtocols.Tls12;
                    handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                    handler.ClientCertificates.Add(clientCertificate);
                }

                return handler;
            })
            .AddHttpMessageHandler<UserContextForwardingHandler>();

        if (options.BearerToken is { } bearer)
        {
            AddBearerTokenHandler(builder, bearer);
        }

        return services;
    }

    private static void AddBearerTokenHandler(IHttpClientBuilder builder, TokenCredentials credentials)
    {
        // Azure.Identity caches and refreshes internally. Capture one credential source
        // per configured HttpClient so the cache survives HttpMessageHandler rotation.
        ClientSecretTokenCredentialSource source = new(credentials);

        builder.AddHttpMessageHandler(() => new BearerTokenHandler(source.Credential, source.Scopes));
    }
}
