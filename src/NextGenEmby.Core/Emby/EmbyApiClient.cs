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
                $"Users/{EscapeUriComponent(session.UserId)}/Items/Latest?IncludeItemTypes=Movie,Series,Episode&Fields=Overview,ProductionYear,RunTimeTicks,PrimaryImageAspectRatio&Limit=50");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<List<ItemDto>>(body, _jsonOptions) ?? new List<ItemDto>();
            return dto.Select(MapItem).ToList();
        }

        public async Task<IReadOnlyList<EmbyMediaSource>> GetPlaybackInfoAsync(EmbySession session, string itemId)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Items/{EscapeUriComponent(itemId)}/PlaybackInfo");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<PlaybackInfoDto>(body, _jsonOptions) ?? new PlaybackInfoDto();
            var mediaSources = dto.MediaSources ?? new List<MediaSourceDto>();
            return mediaSources.Select(source => MapMediaSource(session, itemId, source)).ToList();
        }

        public string GetImageUrl(EmbySession session, string itemId, string imageType, int maxWidth)
        {
            return
                $"{session.ServerUrl.TrimEnd('/')}/Items/{EscapeUriComponent(itemId)}/Images/{EscapeUriComponent(imageType)}" +
                $"?maxWidth={maxWidth}&quality=90&api_key={EscapeUriComponent(session.AccessToken)}";
        }

        private static EmbyMediaItem MapItem(ItemDto item)
        {
            var imageTags = item.ImageTags;
            var backdropImageTags = item.BackdropImageTags;

            return new EmbyMediaItem
            {
                Id = item.Id,
                Name = item.Name,
                Type = item.Type,
                Overview = item.Overview,
                ProductionYear = item.ProductionYear,
                RunTimeTicks = item.RunTimeTicks,
                PrimaryImageTag = imageTags != null && imageTags.TryGetValue("Primary", out var primary) ? primary : "",
                BackdropImageTag = backdropImageTags != null && backdropImageTags.Count > 0 ? backdropImageTags[0] : ""
            };
        }

        private static EmbyMediaSource MapMediaSource(EmbySession session, string itemId, MediaSourceDto source)
        {
            var id = source.Id ?? "";
            var result = new EmbyMediaSource
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(source.Name) ? id : source.Name,
                Container = source.Container ?? "",
                Bitrate = source.Bitrate,
                DirectStreamUrl =
                    $"{session.ServerUrl.TrimEnd('/')}/Videos/{EscapeUriComponent(itemId)}/stream" +
                    $"?static=true&mediaSourceId={EscapeUriComponent(id)}&api_key={EscapeUriComponent(session.AccessToken)}"
            };

            var streams = source.MediaStreams ?? new List<MediaStreamDto>();
            foreach (var stream in streams)
            {
                var kind = ParseStreamKind(stream.Type);
                result.Streams.Add(new EmbyMediaStream
                {
                    Index = stream.Index,
                    Kind = kind,
                    Codec = stream.Codec ?? "",
                    Language = stream.Language ?? "",
                    ChannelLayout = stream.ChannelLayout ?? "",
                    DisplayTitle = stream.DisplayTitle ?? "",
                    IsExternal = stream.IsExternal
                });

                if (kind == EmbyStreamKind.Video)
                {
                    result.Width = stream.Width;
                    result.Height = stream.Height;
                    var videoRange = stream.VideoRange ?? "";
                    result.IsHdr = videoRange.IndexOf("HDR", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }

            return result;
        }

        private static EmbyStreamKind ParseStreamKind(string type)
        {
            if (string.Equals(type, "Video", StringComparison.OrdinalIgnoreCase))
            {
                return EmbyStreamKind.Video;
            }

            if (string.Equals(type, "Audio", StringComparison.OrdinalIgnoreCase))
            {
                return EmbyStreamKind.Audio;
            }

            return EmbyStreamKind.Subtitle;
        }

        private static string EscapeUriComponent(string value)
        {
            return Uri.EscapeDataString(value);
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

        private sealed class PlaybackInfoDto
        {
            public List<MediaSourceDto> MediaSources { get; set; } = new List<MediaSourceDto>();
        }

        private sealed class MediaSourceDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Container { get; set; } = "";
            public long Bitrate { get; set; }
            public List<MediaStreamDto> MediaStreams { get; set; } = new List<MediaStreamDto>();
        }

        private sealed class MediaStreamDto
        {
            public int Index { get; set; }
            public string Type { get; set; } = "";
            public string Codec { get; set; } = "";
            public string Language { get; set; } = "";
            public string ChannelLayout { get; set; } = "";
            public string DisplayTitle { get; set; } = "";
            public bool IsExternal { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public string VideoRange { get; set; } = "";
        }
    }
}
