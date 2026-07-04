using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyApiClient
    {
        private readonly HttpClient _http;
        private readonly EmbyClientOptions _options;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public EmbyApiClient(HttpClient http, EmbyClientOptions options)
        {
            _http = http;
            _options = options;
            _http.BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/");
        }

        public async Task<EmbySession> AuthenticateAsync(string username, string password)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "Users/AuthenticateByName");
            ApplyAuthorizationHeader(request, null);
            var json = JsonSerializer.Serialize(new { Username = username, Pw = password });
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<AuthResponseDto>(body, _jsonOptions)
                ?? throw new InvalidOperationException("Emby authentication response was empty.");

            return new EmbySession
            {
                ServerUrl = _options.ServerUrl.TrimEnd('/'),
                UserId = dto.User.Id,
                UserName = dto.User.Name,
                AccessToken = dto.AccessToken
            };
        }

        private void ApplyAuthorizationHeader(HttpRequestMessage request, string? token)
        {
            var value =
                $"Client=\"{_options.ClientName}\", " +
                $"Device=\"{_options.DeviceName}\", " +
                $"DeviceId=\"{_options.DeviceId}\", " +
                $"Version=\"{_options.ClientVersion}\"";

            if (!string.IsNullOrWhiteSpace(token))
            {
                value += $", Token=\"{token}\"";
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Emby", value);
        }

        private sealed class AuthResponseDto
        {
            public string AccessToken { get; set; } = "";
            public UserDto User { get; set; } = new UserDto();
        }

        private sealed class UserDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
        }
    }
}
