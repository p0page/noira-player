using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NoiraPlayer.Core.Tests;

public sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    public TestHttpRequestSnapshot? LastRequest { get; private set; }

    public int RequestCount { get; private set; }

    public bool IsDisposed { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync().ConfigureAwait(false);

        LastRequest = TestHttpRequestSnapshot.From(request, body);
        RequestCount++;
        return _handler(request);
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }
}

public sealed class TestHttpRequestSnapshot
{
    private TestHttpRequestSnapshot(
        HttpMethod method,
        Uri? requestUri,
        string? authorizationScheme,
        string? authorizationParameter,
        string? acceptMediaType,
        string? contentTypeMediaType,
        string? contentTypeCharSet,
        string? embyToken,
        string? body)
    {
        Method = method;
        RequestUri = requestUri;
        AuthorizationScheme = authorizationScheme;
        AuthorizationParameter = authorizationParameter;
        AcceptMediaType = acceptMediaType;
        ContentTypeMediaType = contentTypeMediaType;
        ContentTypeCharSet = contentTypeCharSet;
        EmbyToken = embyToken;
        Body = body;
    }

    public HttpMethod Method { get; }
    public Uri? RequestUri { get; }
    public string? AuthorizationScheme { get; }
    public string? AuthorizationParameter { get; }
    public string? AcceptMediaType { get; }
    public string? ContentTypeMediaType { get; }
    public string? ContentTypeCharSet { get; }
    public string? EmbyToken { get; }
    public string? Body { get; }

    public static TestHttpRequestSnapshot From(HttpRequestMessage request, string? body)
    {
        var embyToken = request.Headers.TryGetValues("X-Emby-Token", out var values)
            ? values.SingleOrDefault()
            : null;

        return new TestHttpRequestSnapshot(
            request.Method,
            request.RequestUri,
            request.Headers.Authorization?.Scheme,
            request.Headers.Authorization?.Parameter,
            request.Headers.Accept.SingleOrDefault()?.MediaType,
            request.Content?.Headers.ContentType?.MediaType,
            request.Content?.Headers.ContentType?.CharSet,
            embyToken,
            body);
    }
}
