using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            EmbyAuthorization.Apply(request, _options);
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

        public async Task<IReadOnlyList<EmbyMediaItem>> GetLatestItemsAsync(EmbySession session)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Users/{session.UserId}/Items/Latest?IncludeItemTypes=Movie,Series,Episode&Fields=Overview,ProductionYear,RunTimeTicks,PrimaryImageAspectRatio&Limit=50");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<List<ItemDto>>(body, _jsonOptions) ?? new List<ItemDto>();
            return dto.Select(MapItem).ToList();
        }

        public string GetImageUrl(EmbySession session, string itemId, string imageType, int maxWidth)
        {
            return $"{session.ServerUrl.TrimEnd('/')}/Items/{itemId}/Images/{imageType}?maxWidth={maxWidth}&quality=90&api_key={session.AccessToken}";
        }

        private static EmbyMediaItem MapItem(ItemDto item)
        {
            return new EmbyMediaItem
            {
                Id = item.Id,
                Name = item.Name,
                Type = item.Type,
                Overview = item.Overview,
                ProductionYear = item.ProductionYear,
                RunTimeTicks = item.RunTimeTicks,
                PrimaryImageTag = item.ImageTags.TryGetValue("Primary", out var primary) ? primary : "",
                BackdropImageTag = item.BackdropImageTags.Count > 0 ? item.BackdropImageTags[0] : ""
            };
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

        private sealed class ItemDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string Overview { get; set; } = "";
            public int? ProductionYear { get; set; }
            public long? RunTimeTicks { get; set; }
            public Dictionary<string, string> ImageTags { get; set; } = new Dictionary<string, string>();
            public List<string> BackdropImageTags { get; set; } = new List<string>();
        }
    }
}
