using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class MediaDetailsPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private EmbyMediaItem? _item;
        private IReadOnlyList<EmbyMediaSource> _mediaSources = Array.Empty<EmbyMediaSource>();

        public MediaDetailsPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var item = e.Parameter as EmbyMediaItem;
            if (item != null)
            {
                _item = item;
                RenderItem();
                await LoadDetailsAsync(item.Id, item.Name);
                return;
            }

            var request = e.Parameter as MediaDetailsNavigationRequest;
            if (request != null)
            {
                _item = new EmbyMediaItem
                {
                    Id = request.ItemId,
                    Name = request.ItemName
                };
                RenderItem();
                await LoadDetailsAsync(request.ItemId, request.ItemName);
                return;
            }

            _item = null;
            RenderItem();
        }

        private void RenderItem()
        {
            ResetPlaybackSections();

            if (_item == null || string.IsNullOrWhiteSpace(_item.Id))
            {
                TitleBlock.Text = "Item unavailable";
                MetaBlock.Text = "";
                OverviewBlock.Text = "";
                StatusBlock.Text = "Go back and choose another item.";
                PlayButton.IsEnabled = false;
                PlayButtonText.Text = "Play";
                return;
            }

            TitleBlock.Text = string.IsNullOrWhiteSpace(_item.Name) ? _item.Id : _item.Name;
            MetaBlock.Text = CreateMeta(_item);
            OverviewBlock.Text = string.IsNullOrWhiteSpace(_item.Overview)
                ? "No overview available."
                : _item.Overview;
            StatusBlock.Text = "";
            PlayButton.IsEnabled = CanPlay(_item);
            PlayButtonText.Text = CreatePlayButtonText(_item);
            if (PlayButton.IsEnabled)
            {
                PlayButton.Focus(FocusState.Programmatic);
            }
        }

        private async Task LoadDetailsAsync(string itemId, string fallbackName)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                _item = null;
                RenderItem();
                return;
            }

            if (_item == null)
            {
                _item = new EmbyMediaItem
                {
                    Id = itemId,
                    Name = fallbackName ?? ""
                };
                RenderItem();
            }

            StatusBlock.Text = "Loading...";

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                using (var http = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(http, session);
                    _item = await client.GetItemAsync(session, itemId);
                    RenderItem();
                    await LoadImagesAsync();
                    await LoadPlaybackInfoAsync(client, session, itemId);
                    await LoadSeriesEpisodesAsync(client, session);
                }
                StatusBlock.Text = "";
            }
            catch
            {
                RenderItem();
                StatusBlock.Text = "Unable to load details.";
            }
        }

        private async Task LoadImagesAsync()
        {
            if (_item == null || string.IsNullOrWhiteSpace(_item.Id))
            {
                return;
            }

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                using (var http = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(http, session);
                    if (!string.IsNullOrWhiteSpace(_item.PrimaryImageTag))
                    {
                        PosterImage.Source = new BitmapImage(new Uri(client.GetImageUrl(session, _item.Id, "Primary", 720)));
                        PosterFallbackBlock.Visibility = Visibility.Collapsed;
                    }

                    if (!string.IsNullOrWhiteSpace(_item.BackdropImageTag))
                    {
                        BackdropImage.Source = new BitmapImage(new Uri(client.GetImageUrl(session, _item.Id, "Backdrop", 1920)));
                    }
                }
            }
            catch
            {
                StatusBlock.Text = "Unable to load artwork.";
            }
        }

        private void Play_OnClick(object sender, RoutedEventArgs e)
        {
            if (_item == null || string.IsNullOrWhiteSpace(_item.Id))
            {
                return;
            }

            if (!CanPlay(_item))
            {
                return;
            }

            var startPositionTicks = _item.UserData == null ? 0 : _item.UserData.PlaybackPositionTicks;
            var mediaSourceId = _mediaSources.FirstOrDefault()?.Id ?? "";
            Frame.Navigate(
                typeof(PlaybackPage),
                new PlaybackLaunchRequest(_item.Id, _item.Name, startPositionTicks, mediaSourceId));
        }

        private async void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            var itemId = _item == null ? "" : _item.Id;
            var itemName = _item == null ? "" : _item.Name;
            await LoadDetailsAsync(itemId, itemName);
        }

        private async Task LoadPlaybackInfoAsync(EmbyApiClient client, EmbySession session, string itemId)
        {
            _mediaSources = Array.Empty<EmbyMediaSource>();
            try
            {
                _mediaSources = await client.GetPlaybackInfoAsync(session, itemId);
                RenderPlaybackInfo();
            }
            catch
            {
                VersionsPanel.Children.Clear();
                AudioSummaryBlock.Text = CanPlay(_item) ? "Audio: unavailable" : "";
                SubtitleSummaryBlock.Text = CanPlay(_item) ? "Subtitles: unavailable" : "";
            }
        }

        private async Task LoadSeriesEpisodesAsync(EmbyApiClient client, EmbySession session)
        {
            EpisodesPanel.Children.Clear();
            EpisodesSection.Visibility = Visibility.Collapsed;
            var item = _item;

            if (item == null ||
                !string.Equals(item.Type, "Series", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            EpisodesSection.Visibility = Visibility.Visible;
            AddEpisodeMessage("Loading episodes...");

            try
            {
                var seasons = await client.GetChildrenAsync(session, item.Id, "Season");
                var firstSeason = seasons.FirstOrDefault(season => !string.IsNullOrWhiteSpace(season.Id));
                EpisodesPanel.Children.Clear();
                if (firstSeason == null)
                {
                    AddEpisodeMessage("No seasons found.");
                    return;
                }

                var episodes = await client.GetChildrenAsync(session, firstSeason.Id, "Episode");
                if (episodes.Count == 0)
                {
                    AddEpisodeMessage("No episodes found.");
                    return;
                }

                foreach (var episode in episodes)
                {
                    AddEpisodeButton(episode);
                }
            }
            catch
            {
                EpisodesPanel.Children.Clear();
                AddEpisodeMessage("Unable to load episodes.");
            }
        }

        private void RenderPlaybackInfo()
        {
            VersionsPanel.Children.Clear();

            if (!CanPlay(_item))
            {
                AudioSummaryBlock.Text = "";
                SubtitleSummaryBlock.Text = "";
                return;
            }

            var header = new TextBlock
            {
                Text = "Versions",
                FontSize = 26,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold
            };
            VersionsPanel.Children.Add(header);

            if (_mediaSources.Count == 0)
            {
                VersionsPanel.Children.Add(CreateMutedText("No playable versions returned."));
                AudioSummaryBlock.Text = "Audio: unavailable";
                SubtitleSummaryBlock.Text = "Subtitles: unavailable";
                return;
            }

            foreach (var source in _mediaSources)
            {
                VersionsPanel.Children.Add(CreateMutedText(CreateSourceSummary(source)));
            }

            AudioSummaryBlock.Text = "Audio: " + CreateAudioSummary(_mediaSources);
            SubtitleSummaryBlock.Text = "Subtitles: " + CreateSubtitleSummary(_mediaSources);
        }

        private void AddEpisodeButton(EmbyMediaItem episode)
        {
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(18, 14, 18, 14),
                Tag = episode,
                UseSystemFocusVisuals = true
            };

            var text = new StackPanel
            {
                Spacing = 4
            };
            text.Children.Add(new TextBlock
            {
                Text = CreateEpisodeTitle(episode),
                FontSize = 20,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.WrapWholeWords
            });
            text.Children.Add(new TextBlock
            {
                Text = CreateMeta(episode),
                FontSize = 16,
                Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AppMutedTextBrush"],
                TextWrapping = TextWrapping.Wrap
            });

            button.Content = text;
            button.Click += Episode_OnClick;
            EpisodesPanel.Children.Add(button);
        }

        private void AddEpisodeMessage(string message)
        {
            EpisodesPanel.Children.Add(CreateMutedText(message));
        }

        private void Episode_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var episode = button?.Tag as EmbyMediaItem;
            if (episode == null || string.IsNullOrWhiteSpace(episode.Id))
            {
                return;
            }

            if (CanPlay(episode))
            {
                var startPositionTicks = episode.UserData == null ? 0 : episode.UserData.PlaybackPositionTicks;
                Frame.Navigate(
                    typeof(PlaybackPage),
                    new PlaybackLaunchRequest(episode.Id, episode.Name, startPositionTicks));
                return;
            }

            Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(episode.Id, episode.Name));
        }

        private void ResetPlaybackSections()
        {
            _mediaSources = Array.Empty<EmbyMediaSource>();
            VersionsPanel.Children.Clear();
            AudioSummaryBlock.Text = "";
            SubtitleSummaryBlock.Text = "";
            EpisodesPanel.Children.Clear();
            EpisodesSection.Visibility = Visibility.Collapsed;
        }

        private static bool CanPlay(EmbyMediaItem? item)
        {
            return item != null
                && !string.IsNullOrWhiteSpace(item.Id)
                && (string.Equals(item.Type, "Movie", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase));
        }

        private static TextBlock CreateMutedText(string text)
        {
            return new TextBlock
            {
                Text = text ?? "",
                FontSize = 20,
                Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AppMutedTextBrush"],
                TextWrapping = TextWrapping.Wrap
            };
        }

        private static string CreatePlayButtonText(EmbyMediaItem item)
        {
            var startPositionTicks = item.UserData == null ? 0 : item.UserData.PlaybackPositionTicks;
            return startPositionTicks > 0 ? "Resume" : "Play";
        }

        private static string CreateSourceSummary(EmbyMediaSource source)
        {
            var label = string.IsNullOrWhiteSpace(source.Name) ? source.Id : source.Name;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(label))
            {
                parts.Add(label);
            }

            if (source.Width > 0 && source.Height > 0)
            {
                parts.Add(source.Width + "x" + source.Height);
            }

            if (source.IsHdr)
            {
                parts.Add("HDR");
            }

            if (!string.IsNullOrWhiteSpace(source.Container))
            {
                parts.Add(source.Container.ToUpperInvariant());
            }

            if (source.Bitrate > 0)
            {
                parts.Add(FormatBitrate(source.Bitrate));
            }

            return parts.Count == 0 ? "Unknown version" : string.Join(" / ", parts);
        }

        private static string CreateAudioSummary(IReadOnlyList<EmbyMediaSource> sources)
        {
            var audio = sources
                .SelectMany(source => source.AudioStreams)
                .Select(CreateStreamSummary)
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();

            return audio.Count == 0 ? "None listed" : string.Join(", ", audio);
        }

        private static string CreateSubtitleSummary(IReadOnlyList<EmbyMediaSource> sources)
        {
            var subtitles = sources
                .SelectMany(source => source.SubtitleStreams)
                .Select(CreateStreamSummary)
                .Where(summary => !string.IsNullOrWhiteSpace(summary))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();

            return subtitles.Count == 0 ? "None listed" : string.Join(", ", subtitles);
        }

        private static string CreateStreamSummary(EmbyMediaStream stream)
        {
            if (!string.IsNullOrWhiteSpace(stream.DisplayTitle))
            {
                return stream.DisplayTitle;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(stream.Language))
            {
                parts.Add(stream.Language);
            }

            if (!string.IsNullOrWhiteSpace(stream.Codec))
            {
                parts.Add(stream.Codec.ToUpperInvariant());
            }

            if (!string.IsNullOrWhiteSpace(stream.ChannelLayout))
            {
                parts.Add(stream.ChannelLayout);
            }

            if (stream.IsExternal)
            {
                parts.Add("External");
            }

            return string.Join(" ", parts);
        }

        private static string CreateEpisodeTitle(EmbyMediaItem episode)
        {
            var title = string.IsNullOrWhiteSpace(episode.Name) ? episode.Id : episode.Name;
            if (episode.ParentIndexNumber.HasValue && episode.IndexNumber.HasValue)
            {
                return "S" + episode.ParentIndexNumber.Value + ":E" + episode.IndexNumber.Value + " " + title;
            }

            if (episode.IndexNumber.HasValue)
            {
                return episode.IndexNumber.Value + ". " + title;
            }

            return title;
        }

        private static string FormatBitrate(long bitrate)
        {
            if (bitrate >= 1000000)
            {
                return Math.Round(bitrate / 1000000d, 1) + " Mbps";
            }

            return Math.Max(1, bitrate / 1000) + " Kbps";
        }

        private static string CreateMeta(EmbyMediaItem item)
        {
            var meta = string.IsNullOrWhiteSpace(item.Type) ? "Item" : item.Type;
            if (item.ProductionYear.HasValue)
            {
                meta += " / " + item.ProductionYear.Value;
            }

            if (item.RunTimeTicks.HasValue && item.RunTimeTicks.Value > 0)
            {
                var runtime = TimeSpan.FromTicks(item.RunTimeTicks.Value);
                if (runtime.TotalHours >= 1)
                {
                    meta += " / " + (int)runtime.TotalHours + "h " + runtime.Minutes + "m";
                }
                else
                {
                    meta += " / " + Math.Max(1, runtime.Minutes) + "m";
                }
            }

            return meta;
        }
    }
}
