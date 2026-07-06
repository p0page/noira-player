using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NextGenEmby.Core.Playback;

namespace NextGenEmby.Core.Emby
{
    public sealed class EmbyApiClient
    {
        private const string ItemListFields = "Overview,ProductionYear,RunTimeTicks,PrimaryImageAspectRatio,ChildCount,UserData";
        private const string ImageTypeList = "Primary,Backdrop,Thumb,Banner,Logo";

        private readonly HttpClient _http;
        private readonly EmbyClientOptions _options;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        private readonly JsonSerializerOptions _writeJsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public EmbyApiClient(HttpClient http, EmbyClientOptions options)
        {
            _http = http;
            _options = options;
            _http.BaseAddress = new Uri(options.ServerUrl.TrimEnd('/') + "/");
            _writeJsonOptions.Converters.Add(new JsonStringEnumConverter());
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
            var parameters = new List<string>();
            AddQueryParameter(parameters, "IncludeItemTypes", "Movie,Series,Episode");
            AddQueryParameter(parameters, "Fields", ItemListFields);
            AddQueryParameter(parameters, "Limit", "50");
            AddImageQueryParameters(parameters);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Users/{EscapeUriComponent(session.UserId)}/Items/Latest?{string.Join("&", parameters)}");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<List<ItemDto>>(body, _jsonOptions) ?? new List<ItemDto>();
            return dto.Select(MapItem).ToList();
        }

        public async Task<IReadOnlyList<EmbyMediaItem>> GetLatestItemsAsync(
            EmbySession session,
            string parentId,
            string includeItemTypes,
            int limit)
        {
            var parameters = new List<string>();
            AddQueryParameter(parameters, "ParentId", parentId);
            AddQueryParameter(parameters, "IncludeItemTypes", includeItemTypes);
            AddQueryParameter(parameters, "Fields", ItemListFields);
            AddQueryParameter(parameters, "Limit", Math.Max(1, limit).ToString());
            AddImageQueryParameters(parameters);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Users/{EscapeUriComponent(session.UserId)}/Items/Latest?{string.Join("&", parameters)}");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<List<ItemDto>>(body, _jsonOptions) ?? new List<ItemDto>();
            return dto.Select(MapItem).ToList();
        }

        public async Task<IReadOnlyList<EmbyMediaItem>> GetResumeItemsAsync(EmbySession session, int limit = 20)
        {
            var parameters = new List<string>();
            AddQueryParameter(parameters, "IncludeItemTypes", "Movie,Episode");
            AddQueryParameter(parameters, "Fields", ItemListFields);
            AddQueryParameter(parameters, "Limit", Math.Max(1, limit).ToString());
            AddImageQueryParameters(parameters);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Users/{EscapeUriComponent(session.UserId)}/Items/Resume?{string.Join("&", parameters)}");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<ItemListDto<ItemDto>>(body, _jsonOptions) ?? new ItemListDto<ItemDto>();
            return (dto.Items ?? new List<ItemDto>()).Select(MapItem).ToList();
        }

        public async Task<IReadOnlyList<EmbyLibraryView>> GetUserViewsAsync(EmbySession session)
        {
            var parameters = new List<string>();
            AddImageQueryParameters(parameters);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Users/{EscapeUriComponent(session.UserId)}/Views?{string.Join("&", parameters)}");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<ItemListDto<ViewDto>>(body, _jsonOptions) ?? new ItemListDto<ViewDto>();
            return (dto.Items ?? new List<ViewDto>()).Select(MapView).ToList();
        }

        public async Task<IReadOnlyList<EmbyHomeSection>> GetHomeSectionsAsync(EmbySession session)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Users/{EscapeUriComponent(session.UserId)}/HomeSections");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<List<HomeSectionDto>>(body, _jsonOptions) ?? new List<HomeSectionDto>();
            return dto.Select(MapHomeSection).ToList();
        }

        public async Task<IReadOnlyList<EmbyMediaItem>> GetHomeSectionItemsAsync(
            EmbySession session,
            string sectionId,
            int limit)
        {
            var parameters = new List<string>();
            AddQueryParameter(parameters, "Limit", Math.Max(1, limit).ToString());
            AddQueryParameter(parameters, "Fields", ItemListFields);
            AddImageQueryParameters(parameters);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Users/{EscapeUriComponent(session.UserId)}/Sections/{EscapeUriComponent(sectionId)}/Items?{string.Join("&", parameters)}");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<ItemListDto<ItemDto>>(body, _jsonOptions) ?? new ItemListDto<ItemDto>();
            return (dto.Items ?? new List<ItemDto>()).Select(MapItem).ToList();
        }

        public async Task<IReadOnlyList<EmbyMediaItem>> GetNextUpItemsAsync(EmbySession session, int limit)
        {
            var parameters = new List<string>();
            AddQueryParameter(parameters, "UserId", session.UserId);
            AddQueryParameter(parameters, "Fields", ItemListFields);
            AddQueryParameter(parameters, "Limit", Math.Max(1, limit).ToString());
            AddImageQueryParameters(parameters);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Shows/NextUp?{string.Join("&", parameters)}");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<ItemListDto<ItemDto>>(body, _jsonOptions) ?? new ItemListDto<ItemDto>();
            return (dto.Items ?? new List<ItemDto>()).Select(MapItem).ToList();
        }

        public Task<IReadOnlyList<EmbyMediaItem>> GetChildrenAsync(
            EmbySession session,
            string parentId,
            string includeItemTypes)
        {
            return GetItemsAsync(session, new EmbyItemsQuery
            {
                ParentId = parentId,
                IncludeItemTypes = includeItemTypes,
                SortBy = "SortName",
                SortOrder = "Ascending",
                Limit = 100,
                Recursive = false
            });
        }

        public async Task<IReadOnlyList<EmbyMediaItem>> GetItemsAsync(EmbySession session, EmbyItemsQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, BuildItemsPath(session, query));
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<ItemListDto<ItemDto>>(body, _jsonOptions) ?? new ItemListDto<ItemDto>();
            return (dto.Items ?? new List<ItemDto>()).Select(MapItem).ToList();
        }

        public Task<IReadOnlyList<EmbyMediaItem>> SearchItemsAsync(
            EmbySession session,
            string searchTerm,
            string includeItemTypes)
        {
            return GetItemsAsync(session, new EmbyItemsQuery
            {
                SearchTerm = searchTerm,
                IncludeItemTypes = includeItemTypes
            });
        }

        public async Task<EmbyMediaItem> GetItemAsync(EmbySession session, string itemId)
        {
            var parameters = new List<string>();
            AddQueryParameter(parameters, "Fields", "Overview,ProductionYear,RunTimeTicks,ChildCount,MediaSources,UserData");
            AddImageQueryParameters(parameters);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Users/{EscapeUriComponent(session.UserId)}/Items/{EscapeUriComponent(itemId)}?{string.Join("&", parameters)}");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<ItemDto>(body, _jsonOptions)
                ?? throw new InvalidOperationException("Emby item response was empty.");
            return MapItem(dto);
        }

        public async Task<IReadOnlyList<EmbyMediaSource>> GetPlaybackInfoAsync(EmbySession session, string itemId)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"Items/{EscapeUriComponent(itemId)}/PlaybackInfo?UserId={EscapeUriComponent(session.UserId)}");
            EmbyAuthorization.Apply(request, _options, session);

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<PlaybackInfoDto>(body, _jsonOptions) ?? new PlaybackInfoDto();
            var mediaSources = dto.MediaSources ?? new List<MediaSourceDto>();
            return mediaSources.Select(source => MapMediaSource(session, itemId, dto.PlaySessionId, source)).ToList();
        }

        public async Task ReportProgressAsync(EmbySession session, PlaybackProgressRequest progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            ValidatePlaybackSessionRequest(progress, nameof(progress));

            if (!Enum.IsDefined(typeof(PlaybackProgressEvent), progress.EventName))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), "Playback progress event is not supported.");
            }

            await PostJsonAsync(session, "Sessions/Playing/Progress", progress).ConfigureAwait(false);
        }

        public Task ReportPlaybackStartAsync(EmbySession session, PlaybackSessionRequest playback)
        {
            return ReportPlaybackSessionAsync(session, playback, "Sessions/Playing", nameof(playback));
        }

        public Task ReportPlaybackStoppedAsync(EmbySession session, PlaybackSessionRequest playback)
        {
            return ReportPlaybackSessionAsync(session, playback, "Sessions/Playing/Stopped", nameof(playback));
        }

        public string GetImageUrl(EmbySession session, string itemId, string imageType, int maxWidth)
        {
            return
                $"{session.ServerUrl.TrimEnd('/')}/Items/{EscapeUriComponent(itemId)}/Images/{EscapeUriComponent(imageType)}" +
                $"?maxWidth={maxWidth}&quality=90&api_key={EscapeUriComponent(session.AccessToken)}";
        }

        private async Task ReportPlaybackSessionAsync(
            EmbySession session,
            PlaybackSessionRequest playback,
            string path,
            string parameterName)
        {
            if (playback == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            ValidatePlaybackSessionRequest(playback, parameterName);
            await PostJsonAsync(session, path, playback).ConfigureAwait(false);
        }

        private void ValidatePlaybackSessionRequest(PlaybackSessionRequest playback, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(playback.ItemId))
            {
                throw new ArgumentException("Playback session reporting requires an item id.", parameterName);
            }

            if (string.IsNullOrWhiteSpace(playback.MediaSourceId))
            {
                throw new ArgumentException("Playback session reporting requires a media source id.", parameterName);
            }

            if (!Enum.IsDefined(typeof(PlaybackPlayMethod), playback.PlayMethod))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Playback play method is not supported.");
            }
        }

        private async Task PostJsonAsync(EmbySession session, string path, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            EmbyAuthorization.Apply(request, _options, session);
            var json = JsonSerializer.Serialize(body, _writeJsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private static string BuildItemsPath(EmbySession session, EmbyItemsQuery query)
        {
            var parameters = new List<string>();
            AddQueryParameter(parameters, "ParentId", query.ParentId);
            AddQueryParameter(parameters, "IncludeItemTypes", query.IncludeItemTypes);
            AddQueryParameter(parameters, "SearchTerm", query.SearchTerm);
            AddQueryParameter(parameters, "SortBy", query.SortBy);
            AddQueryParameter(parameters, "SortOrder", query.SortOrder);
            AddQueryParameter(parameters, "Filters", query.Filters);
            AddQueryParameter(parameters, "StartIndex", Math.Max(0, query.StartIndex).ToString());
            AddQueryParameter(parameters, "Limit", Math.Max(1, query.Limit).ToString());
            AddQueryParameter(parameters, "Recursive", query.Recursive ? "true" : "false");
            AddQueryParameter(parameters, "Fields", ItemListFields);
            AddImageQueryParameters(parameters);

            return $"Users/{EscapeUriComponent(session.UserId)}/Items?{string.Join("&", parameters)}";
        }

        private static void AddImageQueryParameters(List<string> parameters)
        {
            AddQueryParameter(parameters, "EnableImages", "true");
            AddQueryParameter(parameters, "EnableImageTypes", ImageTypeList);
            AddQueryParameter(parameters, "ImageTypeLimit", "1");
        }

        private static void AddQueryParameter(List<string> parameters, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            parameters.Add($"{name}={EscapeUriComponent(value)}");
        }

        private static EmbyLibraryView MapView(ViewDto view)
        {
            var imageTags = view.ImageTags;
            var backdropImageTags = view.BackdropImageTags;
            return new EmbyLibraryView
            {
                Id = view.Id ?? "",
                Name = view.Name ?? "",
                CollectionType = view.CollectionType ?? "",
                ThumbImageTag = imageTags != null && imageTags.TryGetValue("Thumb", out var thumb) ? thumb ?? "" : view.ParentThumbImageTag ?? "",
                PrimaryImageTag = imageTags != null && imageTags.TryGetValue("Primary", out var primary) ? primary ?? "" : "",
                BackdropImageTag = backdropImageTags != null && backdropImageTags.Count > 0 ? backdropImageTags[0] ?? "" : "",
                BannerImageTag = imageTags != null && imageTags.TryGetValue("Banner", out var banner) ? banner ?? "" : "",
                LogoImageTag = imageTags != null && imageTags.TryGetValue("Logo", out var logo) ? logo ?? "" : "",
                ThumbImageItemId = view.ParentThumbItemId ?? "",
                PrimaryImageItemId = view.PrimaryImageItemId ?? "",
                BackdropImageItemId = view.ParentBackdropItemId ?? "",
                BannerImageItemId = view.ParentBannerItemId ?? "",
                LogoImageItemId = view.ParentLogoItemId ?? ""
            };
        }

        private static EmbyHomeSection MapHomeSection(HomeSectionDto section)
        {
            return new EmbyHomeSection
            {
                Id = section.Id ?? "",
                Name = section.Name ?? "",
                Subtitle = section.Subtitle ?? "",
                SectionType = section.SectionType ?? "",
                CollectionType = section.CollectionType ?? "",
                ViewType = section.ViewType ?? "",
                ScrollDirection = section.ScrollDirection ?? "",
                ParentItem = section.ParentItem == null ? new EmbyMediaItem() : MapItem(section.ParentItem)
            };
        }

        private static EmbyMediaItem MapItem(ItemDto item)
        {
            var imageTags = item.ImageTags;
            var backdropImageTags = item.BackdropImageTags;
            var userData = item.UserData ?? new UserDataDto();

            return new EmbyMediaItem
            {
                Id = item.Id ?? "",
                Name = item.Name ?? "",
                Type = item.Type ?? "",
                Overview = item.Overview ?? "",
                ProductionYear = item.ProductionYear,
                RunTimeTicks = item.RunTimeTicks,
                ThumbImageTag = imageTags != null && imageTags.TryGetValue("Thumb", out var thumb) ? thumb ?? "" : item.ParentThumbImageTag ?? "",
                PrimaryImageTag = imageTags != null && imageTags.TryGetValue("Primary", out var primary) ? primary ?? "" : "",
                BackdropImageTag = backdropImageTags != null && backdropImageTags.Count > 0 ? backdropImageTags[0] ?? "" : "",
                BannerImageTag = imageTags != null && imageTags.TryGetValue("Banner", out var banner) ? banner ?? "" : "",
                LogoImageTag = imageTags != null && imageTags.TryGetValue("Logo", out var logo) ? logo ?? "" : "",
                ThumbImageItemId = item.ParentThumbItemId ?? "",
                PrimaryImageItemId = item.PrimaryImageItemId ?? "",
                BackdropImageItemId = item.ParentBackdropItemId ?? "",
                BannerImageItemId = item.ParentBannerItemId ?? "",
                LogoImageItemId = item.ParentLogoItemId ?? "",
                ParentId = item.ParentId ?? "",
                SeriesId = item.SeriesId ?? "",
                IndexNumber = item.IndexNumber,
                ParentIndexNumber = item.ParentIndexNumber,
                ChildCount = item.ChildCount,
                UserData = new EmbyUserData
                {
                    Played = userData.Played,
                    PlaybackPositionTicks = userData.PlaybackPositionTicks,
                    PlayedPercentage = userData.PlayedPercentage
                }
            };
        }

        private static EmbyMediaSource MapMediaSource(
            EmbySession session,
            string itemId,
            string playbackInfoPlaySessionId,
            MediaSourceDto source)
        {
            var id = source.Id ?? "";
            var playSessionId = string.IsNullOrWhiteSpace(source.PlaySessionId)
                ? playbackInfoPlaySessionId
                : source.PlaySessionId;
            var result = new EmbyMediaSource
            {
                Id = id,
                Name = string.IsNullOrWhiteSpace(source.Name) ? id : source.Name,
                Container = source.Container ?? "",
                Bitrate = source.Bitrate,
                PlaySessionId = playSessionId,
                DirectStreamUrl = BuildDirectStreamUrl(session, itemId, playSessionId, source)
            };

            var streams = source.MediaStreams ?? new List<MediaStreamDto>();
            foreach (var stream in streams)
            {
                EmbyStreamKind kind;
                if (!TryParseStreamKind(stream.Type, out kind))
                {
                    continue;
                }

                var mediaStream = new EmbyMediaStream
                {
                    Index = stream.Index,
                    Kind = kind,
                    Codec = stream.Codec ?? "",
                    Language = stream.Language ?? "",
                    ChannelLayout = stream.ChannelLayout ?? "",
                    DisplayTitle = stream.DisplayTitle ?? "",
                    IsExternal = stream.IsExternal,
                    RealFrameRate = stream.RealFrameRate,
                    AverageFrameRate = stream.AverageFrameRate
                };
                result.Streams.Add(mediaStream);

                if (kind == EmbyStreamKind.Video)
                {
                    result.Width = stream.Width;
                    result.Height = stream.Height;
                    result.VideoFrameRate = SelectVideoFrameRate(mediaStream);
                    result.HdrProfile = HdrPlaybackProfileClassifier.Classify(
                        stream.VideoRange ?? "",
                        stream.ColorPrimaries ?? "",
                        stream.ColorTransfer ?? "",
                        stream.ColorSpace ?? "",
                        stream.Codec ?? "",
                        stream.DisplayTitle ?? "",
                        source.Name ?? "");
                }
            }

            return result;
        }

        private static double SelectVideoFrameRate(EmbyMediaStream stream)
        {
            if (stream.RealFrameRate > 0)
            {
                return stream.RealFrameRate;
            }

            return stream.AverageFrameRate > 0 ? stream.AverageFrameRate : 0;
        }

        private static bool TryParseStreamKind(string type, out EmbyStreamKind kind)
        {
            if (string.Equals(type, "Video", StringComparison.OrdinalIgnoreCase))
            {
                kind = EmbyStreamKind.Video;
                return true;
            }

            if (string.Equals(type, "Audio", StringComparison.OrdinalIgnoreCase))
            {
                kind = EmbyStreamKind.Audio;
                return true;
            }

            if (string.Equals(type, "Subtitle", StringComparison.OrdinalIgnoreCase))
            {
                kind = EmbyStreamKind.Subtitle;
                return true;
            }

            kind = default(EmbyStreamKind);
            return false;
        }

        private static string BuildDirectStreamUrl(
            EmbySession session,
            string itemId,
            string playSessionId,
            MediaSourceDto source)
        {
            if (!string.IsNullOrWhiteSpace(source.DirectStreamUrl))
            {
                var directStreamUrl = ResolveDirectStreamUrl(session, source.DirectStreamUrl);
                if (source.AddApiKeyToDirectStreamUrl && !HasQueryParameter(directStreamUrl, "api_key"))
                {
                    directStreamUrl = AppendQueryParameter(
                        directStreamUrl,
                        "api_key",
                        EscapeUriComponent(session.AccessToken));
                }

                return directStreamUrl;
            }

            var url =
                $"{session.ServerUrl.TrimEnd('/')}/Videos/{EscapeUriComponent(itemId)}/stream" +
                $"?static=true&mediaSourceId={EscapeUriComponent(source.Id ?? "")}&api_key={EscapeUriComponent(session.AccessToken)}";

            if (!string.IsNullOrWhiteSpace(source.Container))
            {
                url = AppendQueryParameter(url, "container", EscapeUriComponent(source.Container));
            }

            if (!string.IsNullOrWhiteSpace(playSessionId))
            {
                url = AppendQueryParameter(url, "PlaySessionId", EscapeUriComponent(playSessionId));
            }

            return url;
        }

        private static string ResolveDirectStreamUrl(EmbySession session, string directStreamUrl)
        {
            Uri uri;
            if (Uri.TryCreate(directStreamUrl, UriKind.Absolute, out uri))
            {
                return directStreamUrl;
            }

            var baseUri = new Uri(session.ServerUrl.TrimEnd('/') + "/");
            return new Uri(baseUri, directStreamUrl).AbsoluteUri;
        }

        private static bool HasQueryParameter(string url, string name)
        {
            var queryStart = url.IndexOf('?');
            if (queryStart < 0)
            {
                return false;
            }

            var fragmentStart = url.IndexOf('#', queryStart);
            var query = fragmentStart < 0
                ? url.Substring(queryStart + 1)
                : url.Substring(queryStart + 1, fragmentStart - queryStart - 1);
            var parameters = query.Split('&');
            foreach (var parameter in parameters)
            {
                var equalsIndex = parameter.IndexOf('=');
                var parameterName = equalsIndex < 0 ? parameter : parameter.Substring(0, equalsIndex);
                if (string.Equals(parameterName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string AppendQueryParameter(string url, string name, string escapedValue)
        {
            var fragmentStart = url.IndexOf('#');
            var urlWithoutFragment = fragmentStart < 0 ? url : url.Substring(0, fragmentStart);
            var fragment = fragmentStart < 0 ? "" : url.Substring(fragmentStart);
            var separator = urlWithoutFragment.IndexOf('?') < 0 ? "?" : "&";
            return $"{urlWithoutFragment}{separator}{name}={escapedValue}{fragment}";
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

        private sealed class ItemListDto<T>
        {
            public List<T> Items { get; set; } = new List<T>();
            public int TotalRecordCount { get; set; }
        }

        private sealed class ViewDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string CollectionType { get; set; } = "";
            public Dictionary<string, string> ImageTags { get; set; } = new Dictionary<string, string>();
            public List<string> BackdropImageTags { get; set; } = new List<string>();
            public string PrimaryImageItemId { get; set; } = "";
            public string ParentBackdropItemId { get; set; } = "";
            public string ParentThumbItemId { get; set; } = "";
            public string ParentBannerItemId { get; set; } = "";
            public string ParentLogoItemId { get; set; } = "";
            public string ParentThumbImageTag { get; set; } = "";
        }

        private sealed class HomeSectionDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Subtitle { get; set; } = "";
            public string SectionType { get; set; } = "";
            public string CollectionType { get; set; } = "";
            public string ViewType { get; set; } = "";
            public string ScrollDirection { get; set; } = "";
            public ItemDto? ParentItem { get; set; }
        }

        private sealed class UserDataDto
        {
            public bool Played { get; set; }
            public long PlaybackPositionTicks { get; set; }
            public double? PlayedPercentage { get; set; }
        }

        private sealed class ItemDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string Overview { get; set; } = "";
            public int? ProductionYear { get; set; }
            public long? RunTimeTicks { get; set; }
            public string ParentId { get; set; } = "";
            public string SeriesId { get; set; } = "";
            public int? IndexNumber { get; set; }
            public int? ParentIndexNumber { get; set; }
            public int? ChildCount { get; set; }
            public UserDataDto UserData { get; set; } = new UserDataDto();
            public Dictionary<string, string> ImageTags { get; set; } = new Dictionary<string, string>();
            public List<string> BackdropImageTags { get; set; } = new List<string>();
            public string PrimaryImageItemId { get; set; } = "";
            public string ParentBackdropItemId { get; set; } = "";
            public string ParentThumbItemId { get; set; } = "";
            public string ParentBannerItemId { get; set; } = "";
            public string ParentLogoItemId { get; set; } = "";
            public string ParentThumbImageTag { get; set; } = "";
        }

        private sealed class PlaybackInfoDto
        {
            public string PlaySessionId { get; set; } = "";
            public List<MediaSourceDto> MediaSources { get; set; } = new List<MediaSourceDto>();
        }

        private sealed class MediaSourceDto
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Container { get; set; } = "";
            public long Bitrate { get; set; }
            public string DirectStreamUrl { get; set; } = "";
            public bool AddApiKeyToDirectStreamUrl { get; set; }
            public string PlaySessionId { get; set; } = "";
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
            public double RealFrameRate { get; set; }
            public double AverageFrameRate { get; set; }
            public string VideoRange { get; set; } = "";
            public string ColorPrimaries { get; set; } = "";
            public string ColorTransfer { get; set; } = "";
            public string ColorSpace { get; set; } = "";
        }
    }
}
