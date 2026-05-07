using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace Umea.se.Toolkit.ExternalService;

public abstract class ExternalServiceBase
{
    protected readonly string HttpClientName;
    protected readonly HttpClient HttpClient;
    protected abstract string PingUrl { get; }

    /// <summary>
    /// JSON serialization options used when serializing request bodies.
    /// Defaults to <see cref="JsonSerializerOptions.Web"/> (camelCase).
    /// Override in subclasses to change serialization behavior for a specific API.
    /// </summary>
    protected virtual JsonSerializerOptions SerializerOptions => JsonSerializerOptions.Web;

    protected ExternalServiceBase(string httpClientName, IHttpClientFactory httpClientFactory)
    {
        HttpClientName = httpClientName;
        HttpClient = httpClientFactory.CreateClient(httpClientName);
    }

    /// <summary>
    /// Sends a HttpGet request yielding the raw return value as string.
    /// Use this for Ping methods or anywhere else the response is a string and not the json representation of a string.
    /// </summary>
    public async Task<string> Ping()
    {
        HttpResponseMessage response = await HttpClient.GetAsync(PingUrl);
        response.EnsureSuccessStatusCode();
        string responseString = await response.Content.ReadAsStringAsync();
        if (responseString != "pong")
        {
            throw new PingFailedException(HttpClientName);
        }

        return responseString;
    }

    protected async Task<T> HttpGet<T>(string requestUrl, IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpResponseMessage response = await SendHttpRequest(HttpMethod.Get, requestUrl, null, headers);
        return await EnsureSuccessAndReadBody<T>(response);
    }

    protected async Task<T> HttpPost<T>(string requestUrl, object? requestBody = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpResponseMessage response = await SendHttpRequest(HttpMethod.Post, requestUrl, requestBody, headers);
        return await EnsureSuccessAndReadBody<T>(response);
    }

    protected async Task HttpPost(string requestUrl, object? requestBody = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpResponseMessage response = await SendHttpRequest(HttpMethod.Post, requestUrl, requestBody, headers);
        EnsureSuccessStatusCode(response);
    }

    protected async Task<T?> HttpPostNullable<T>(string requestUrl, object? requestBody = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpResponseMessage response = await SendHttpRequest(HttpMethod.Post, requestUrl, requestBody, headers);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default;
        }

        return await EnsureSuccessAndReadBody<T>(response);
    }
    protected async Task<T> HttpPostContent<T>(string requestUrl, HttpContent content, IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpResponseMessage response = await SendHttpRequest(HttpMethod.Post, requestUrl, content, headers);
        return await EnsureSuccessAndReadBody<T>(response);
    }

    protected async Task<T> HttpPut<T>(string requestUrl, object? requestBody = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpResponseMessage response = await SendHttpRequest(HttpMethod.Put, requestUrl, requestBody, headers);
        return await EnsureSuccessAndReadBody<T>(response);
    }

    protected async Task HttpPut(string requestUrl, object? requestBody = null, IReadOnlyDictionary<string, string>? headers = null)
    {
        HttpResponseMessage response = await SendHttpRequest(HttpMethod.Put, requestUrl, requestBody, headers);
        EnsureSuccessStatusCode(response);
    }

    protected Dictionary<string, string> GetHeadersWithBearerAuthorization(string accessToken)
    {
        return new Dictionary<string, string>
        {
            [HeaderNames.Authorization] = $"Bearer {accessToken}",
        };
    }

    private async Task<HttpResponseMessage> SendHttpRequest(
        HttpMethod httpMethod,
        string endpoint,
        object? requestBody,
        IReadOnlyDictionary<string, string>? headers)
    {
        HttpRequestMessage request = new()
        {
            Method = httpMethod,
            RequestUri = new Uri(endpoint, UriKind.RelativeOrAbsolute),
        };

        if (requestBody is HttpContent httpContent)
        {
            request.Content = httpContent;
        }
        else if (requestBody is not null)
        {
            string json = JsonSerializer.Serialize(requestBody, SerializerOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> header in headers)
            {
                bool isContentHeader = header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Content-Language", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase);

                if (isContentHeader)
                {
                    request.Content ??= new ByteArrayContent([]);
                    request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        return await HttpClient.SendAsync(request);
    }

    private async Task<T> EnsureSuccessAndReadBody<T>(HttpResponseMessage response)
    {
        EnsureSuccessStatusCode(response);

        try
        {
            return await response.Content.ReadFromJsonAsync<T>()
                ?? throw new ArgumentNullException(nameof(response.Content));
        }
        catch (Exception e) when (e is JsonException or ArgumentNullException)
        {
            throw new UnsuccessfulHttpCallException($"Malformed response; cannot deserialize to {typeof(T).FullName}", e, response.StatusCode);
        }
    }

    private void EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            throw new UnsuccessfulHttpCallException($"Unsuccessful call to {HttpClientName}", e, e.StatusCode);
        }
    }
}
