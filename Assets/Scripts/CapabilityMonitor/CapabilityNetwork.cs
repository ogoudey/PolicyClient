using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Thrown when the gateway returns a non-success status code
/// (the equivalent of requests' raise_for_status()).
/// </summary>
public sealed class CapabilityNetworkException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ResponseBody { get; }

    public CapabilityNetworkException(HttpStatusCode statusCode, string reasonPhrase, string responseBody, string url)
        : base($"{(int)statusCode} {reasonPhrase} for url: {url}" +
               (string.IsNullOrEmpty(responseBody) ? "" : $"\n{responseBody}"))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

/// <summary>
/// Client for the capability gateway: list, describe, publish, update status,
/// read status, and delete capabilities.
/// </summary>
public sealed class CapabilityNetwork : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly string _gatewayUrl;
    private readonly Func<CancellationToken, Task<string>> _tokenProvider;

    /// <param name="gatewayUrl">Base URL of the gateway, e.g. "https://gateway.example.com".</param>
    /// <param name="tokenProvider">Returns the current access token (the equivalent of auth.load_token()).</param>
    /// <param name="httpClient">Optional shared HttpClient. If null, one is created and owned by this instance.</param>
    public CapabilityNetwork(
        string gatewayUrl,
        Func<CancellationToken, Task<string>> tokenProvider,
        HttpClient httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(gatewayUrl))
            throw new ArgumentException("Gateway URL is required.", nameof(gatewayUrl));

        _gatewayUrl = gatewayUrl.TrimEnd('/');
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _ownsClient = httpClient == null;
        _http = httpClient ?? new HttpClient();
    }

    /// <summary>Convenience constructor for a synchronous token source.</summary>
    public CapabilityNetwork(string gatewayUrl, Func<string> tokenProvider, HttpClient httpClient = null)
        : this(gatewayUrl,
               _ => Task.FromResult((tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider)))()),
               httpClient)
    {
    }

    // GET /capabilities[?detail=1]
    public async Task<JArray> ListCapabilitiesAsync(bool detail = false, CancellationToken ct = default)
    {
        var path = detail ? "/capabilities?detail=1" : "/capabilities";
        var body = await SendAsync(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
        return JArray.Parse(body);
    }

    // GET /capabilities/{id}  -> raw text
    public Task<string> DescribeCapabilityAsync(string capabilityId, CancellationToken ct = default)
    {
        return SendAsync(HttpMethod.Get, $"/capabilities/{Escape(capabilityId)}", null, ct);
    }

    // POST /capabilities/{id}/sync  (multipart upload, field "file", text/markdown)
    public async Task<JObject> PublishCapabilityAsync(string path, string capabilityId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("File path is required.", nameof(path));

        using (var stream = File.OpenRead(path))
        using (var form = new MultipartFormDataContent())
        {
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
            form.Add(fileContent, "file", Path.GetFileName(path));

            var body = await SendAsync(HttpMethod.Post, $"/capabilities/{Escape(capabilityId)}/sync", form, ct)
                .ConfigureAwait(false);
            return JObject.Parse(body);
        }
    }

    // POST /capabilities/{id}/sync, uploading in-memory markdown instead of a file on disk
    public async Task<JObject> PublishCapabilityContentAsync(string content, string fileName, string capabilityId, CancellationToken ct = default)
    {
        using (var form = new MultipartFormDataContent())
        {
            var fileContent = new StringContent(content ?? "", Encoding.UTF8);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown") { CharSet = "utf-8" };
            form.Add(fileContent, "file", fileName);

            var body = await SendAsync(HttpMethod.Post, $"/capabilities/{Escape(capabilityId)}/sync", form, ct)
                .ConfigureAwait(false);
            return JObject.Parse(body);
        }
    }

    // PUT /capabilities/{id}/status  (message is sent as-is with Content-Type: application/json)
    public async Task<JObject> UpdateCapabilityStatusAsync(string capabilityId, string message, CancellationToken ct = default)
    {
        using (var content = new StringContent(message ?? "", Encoding.UTF8, "application/json"))
        {
            var body = await SendAsync(HttpMethod.Put, $"/capabilities/{Escape(capabilityId)}/status", content, ct)
                .ConfigureAwait(false);
            return JObject.Parse(body);
        }
    }

    /// <summary>Overload that serializes a JSON object for you.</summary>
    public Task<JObject> UpdateCapabilityStatusAsync(string capabilityId, JToken message, CancellationToken ct = default)
    {
        return UpdateCapabilityStatusAsync(capabilityId, message?.ToString(Formatting.None), ct);
    }

    // GET /capabilities/{id}/status
    public async Task<JObject> SeeCapabilityStatusAsync(string capabilityId, CancellationToken ct = default)
    {
        var body = await SendAsync(HttpMethod.Get, $"/capabilities/{Escape(capabilityId)}/status", null, ct)
            .ConfigureAwait(false);
        return JObject.Parse(body);
    }

    // DELETE /capabilities/{id}
    public Task DeleteCapabilityAsync(string capabilityId, CancellationToken ct = default)
    {
        return SendAsync(HttpMethod.Delete, $"/capabilities/{Escape(capabilityId)}", null, ct);
    }

    /// <summary>
    /// Sends a request with the bearer token attached, throws on non-success,
    /// and returns the response body as a string.
    /// </summary>
    private async Task<string> SendAsync(HttpMethod method, string relativePath, HttpContent content, CancellationToken ct)
    {
        var url = _gatewayUrl + relativePath;
        var token = await _tokenProvider(ct).ConfigureAwait(false);

        using (var request = new HttpRequestMessage(method, url))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;

            using (var response = await _http.SendAsync(request, ct).ConfigureAwait(false))
            {
                var body = response.Content == null
                    ? string.Empty
                    : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                    throw new CapabilityNetworkException(response.StatusCode, response.ReasonPhrase, body, url);

                return body;
            }
        }
    }

    private static string Escape(string capabilityId)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
            throw new ArgumentException("Capability ID is required.", nameof(capabilityId));
        return Uri.EscapeDataString(capabilityId);
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }
}