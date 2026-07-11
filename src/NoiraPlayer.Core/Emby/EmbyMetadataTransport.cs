using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NoiraPlayer.Core.Emby
{
    public sealed class EmbyMetadataTransport : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly bool _disposeHttpClient;
        private bool _disposed;

        public EmbyMetadataTransport(HttpClient httpClient, bool disposeHttpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _disposeHttpClient = disposeHttpClient;
        }

        public static EmbyMetadataTransport CreateDefault()
        {
            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.GZip |
                    DecompressionMethods.Deflate |
                    DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
            };
            var httpClient = new HttpClient(handler)
            {
                Timeout = EmbyRequestTimeoutPolicy.InteractiveRequestTimeout,
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };
            return new EmbyMetadataTransport(httpClient, disposeHttpClient: true);
        }

        public async Task<EmbyMetadataResponse> GetAsync(
            Uri requestUri,
            EmbyClientOptions options,
            EmbySession session,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(requestUri);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(session);
            ValidateRequestUri(requestUri, session);

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            EmbyAuthorization.Apply(request, options, session);

            var startedAt = Stopwatch.GetTimestamp();
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            var bodyLengthBytes = response.Content.Headers.ContentLength ?? Encoding.UTF8.GetByteCount(body);

            return new EmbyMetadataResponse(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "",
                body,
                elapsed.TotalMilliseconds,
                bodyLengthBytes);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_disposeHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private static void ValidateRequestUri(Uri requestUri, EmbySession session)
        {
            if (!requestUri.IsAbsoluteUri ||
                (requestUri.Scheme != Uri.UriSchemeHttp && requestUri.Scheme != Uri.UriSchemeHttps) ||
                !Uri.TryCreate(session.ServerUrl.TrimEnd('/') + "/", UriKind.Absolute, out var serverBase) ||
                Uri.Compare(
                    requestUri,
                    serverBase,
                    UriComponents.SchemeAndServer,
                    UriFormat.SafeUnescaped,
                    StringComparison.OrdinalIgnoreCase) != 0 ||
                !requestUri.AbsolutePath.StartsWith(serverBase.AbsolutePath, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The metadata request URI must stay within the saved Emby server.",
                    nameof(requestUri));
            }
        }
    }

    public sealed class EmbyMetadataResponse
    {
        public EmbyMetadataResponse(
            int statusCode,
            string reasonPhrase,
            string body,
            double networkDurationMilliseconds,
            long bodyLengthBytes)
        {
            StatusCode = statusCode;
            ReasonPhrase = reasonPhrase ?? "";
            Body = body ?? "";
            NetworkDurationMilliseconds = networkDurationMilliseconds;
            BodyLengthBytes = bodyLengthBytes;
        }

        public int StatusCode { get; }
        public string ReasonPhrase { get; }
        public string Body { get; }
        public double NetworkDurationMilliseconds { get; }
        public long BodyLengthBytes { get; }
    }
}
