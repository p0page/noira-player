using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Input;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class MusicPage : Page, ITvContentFocusTarget
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private Button? _firstAlbumButton;
        private Button? _firstSongButton;
        private MusicNavigationRequest? _request;
        private int _loadGeneration;
        private bool _isUnloaded;

        public MusicPage()
        {
            InitializeComponent();
            AddHandler(KeyDownEvent, new KeyEventHandler(MusicPage_OnKeyDown), true);
            Loaded += MusicPage_OnLoaded;
            Unloaded += MusicPage_OnUnloaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isUnloaded = false;
            _request = e.Parameter as MusicNavigationRequest;
        }

        private async void MusicPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MusicPage_OnLoaded;
            if (_request != null && !string.IsNullOrWhiteSpace(_request.UnsupportedSongName))
            {
                PrepareBrowseOnlyPreview();
                StatusBlock.Text = "Browse-only preview";
                ShowUnsupportedPanel(_request.UnsupportedSongName);
                return;
            }

            await LoadMusicAsync();
        }

        private void MusicPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _loadGeneration++;
        }

        public bool FocusDefaultContent()
        {
            if (UnsupportedPanel.Visibility == Visibility.Visible)
            {
                return UnsupportedCloseButton.Focus(FocusState.Keyboard);
            }

            if (FallbackPanel.Visibility == Visibility.Visible)
            {
                return FallbackRetryButton.Focus(FocusState.Keyboard);
            }

            if (_firstAlbumButton != null && _firstAlbumButton.Focus(FocusState.Keyboard))
            {
                return true;
            }

            if (_firstSongButton != null && _firstSongButton.Focus(FocusState.Keyboard))
            {
                return true;
            }

            return RefreshButton.Focus(FocusState.Keyboard);
        }

        private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            await LoadMusicAsync();
        }

        private async void AllSongsButton_OnClick(object sender, RoutedEventArgs e)
        {
            await LoadAllSongsAsync(focusSongs: true);
        }

        private async Task LoadMusicAsync()
        {
            var loadGeneration = ++_loadGeneration;
            ResetMusicSurface();
            SetLoadingState(isLoading: true);

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                if (session == null)
                {
                    ShowFallback("Sign in first", "Open Settings or restart the app after signing in.");
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var albumsTask = client.GetItemsAsync(session, MusicBrowseQueryFactory.CreateAlbumsQuery());
                    var songsTask = client.GetItemsAsync(session, MusicBrowseQueryFactory.CreateSongsQuery());
                    await Task.WhenAll(albumsTask, songsTask);

                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    var albums = MusicBrowseItemPolicy.KeepAlbums(albumsTask.Result);
                    var songs = MusicBrowseItemPolicy.KeepSongs(songsTask.Result);
                    RenderAlbums(session, client, albums);
                    RenderSongs(session, client, songs, "Songs", showAllSongsButton: false);
                    StatusBlock.Text = CreateMusicStatus(albums.Count, songs.Count);

                    if (albums.Count == 0 && songs.Count == 0)
                    {
                        ShowFallback("No music found", "This server did not return albums or songs.");
                        return;
                    }

                    FocusDefaultContent();
                }
            }
            catch
            {
                if (CanApplyLoad(loadGeneration))
                {
                    ShowFallback("Music unavailable", "This server did not return music items.");
                }
            }
            finally
            {
                if (CanApplyLoad(loadGeneration))
                {
                    SetLoadingState(isLoading: false);
                }
            }
        }

        private async Task LoadAllSongsAsync(bool focusSongs)
        {
            var loadGeneration = ++_loadGeneration;
            SongsPanel.Children.Clear();
            _firstSongButton = null;
            SongsTitleBlock.Text = "Songs";
            SongsCountBlock.Text = "Loading";
            AllSongsButton.Visibility = Visibility.Collapsed;
            SetLoadingState(isLoading: true);

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                if (session == null)
                {
                    ShowFallback("Sign in first", "Open Settings or restart the app after signing in.");
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var songs = MusicBrowseItemPolicy.KeepSongs(
                        await client.GetItemsAsync(session, MusicBrowseQueryFactory.CreateSongsQuery()));
                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    RenderSongs(session, client, songs, "Songs", showAllSongsButton: false);
                    if (focusSongs && _firstSongButton != null)
                    {
                        _firstSongButton.Focus(FocusState.Keyboard);
                    }
                }
            }
            catch
            {
                if (CanApplyLoad(loadGeneration))
                {
                    RenderSongsEmpty("Unable to load songs.");
                }
            }
            finally
            {
                if (CanApplyLoad(loadGeneration))
                {
                    SetLoadingState(isLoading: false);
                }
            }
        }

        private async Task LoadAlbumSongsAsync(EmbyMediaItem album)
        {
            if (album == null || string.IsNullOrWhiteSpace(album.Id))
            {
                return;
            }

            var loadGeneration = ++_loadGeneration;
            SongsPanel.Children.Clear();
            _firstSongButton = null;
            SongsTitleBlock.Text = CreateItemName(album);
            SongsCountBlock.Text = "Loading";
            AllSongsButton.Visibility = Visibility.Visible;
            SetLoadingState(isLoading: true);

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                if (session == null)
                {
                    ShowFallback("Sign in first", "Open Settings or restart the app after signing in.");
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var songs = MusicBrowseItemPolicy.KeepSongs(
                        await client.GetItemsAsync(session, MusicBrowseQueryFactory.CreateAlbumSongsQuery(album.Id)));
                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    RenderSongs(session, client, songs, CreateItemName(album), showAllSongsButton: true);
                    if (_firstSongButton != null)
                    {
                        _firstSongButton.Focus(FocusState.Keyboard);
                    }
                    else
                    {
                        AllSongsButton.Focus(FocusState.Keyboard);
                    }
                }
            }
            catch
            {
                if (CanApplyLoad(loadGeneration))
                {
                    RenderSongsEmpty("Unable to load album songs.");
                    AllSongsButton.Visibility = Visibility.Visible;
                    AllSongsButton.Focus(FocusState.Keyboard);
                }
            }
            finally
            {
                if (CanApplyLoad(loadGeneration))
                {
                    SetLoadingState(isLoading: false);
                }
            }
        }

        private void ResetMusicSurface()
        {
            FallbackPanel.Visibility = Visibility.Collapsed;
            UnsupportedPanel.Visibility = Visibility.Collapsed;
            AlbumsPanel.Children.Clear();
            SongsPanel.Children.Clear();
            _firstAlbumButton = null;
            _firstSongButton = null;
            AlbumsCountBlock.Text = "Loading";
            SongsTitleBlock.Text = "Songs";
            SongsCountBlock.Text = "Loading";
            AllSongsButton.Visibility = Visibility.Collapsed;
            PreviewBadgeBlock.Text = "Now";
            PreviewTitleBlock.Text = "Select music";
            PreviewBodyBlock.Text = "Albums and songs appear here when the server exposes a music library.";
            StatusBlock.Text = "Loading music";
        }

        private void PrepareBrowseOnlyPreview()
        {
            FallbackPanel.Visibility = Visibility.Collapsed;
            UnsupportedPanel.Visibility = Visibility.Collapsed;
            AlbumsPanel.Children.Clear();
            SongsPanel.Children.Clear();
            _firstAlbumButton = null;
            _firstSongButton = null;
            AlbumsCountBlock.Text = "Refresh to load";
            SongsTitleBlock.Text = "Songs";
            SongsCountBlock.Text = "Refresh to load";
            AllSongsButton.Visibility = Visibility.Collapsed;
            PreviewBadgeBlock.Text = "Now";
            PreviewTitleBlock.Text = "Select music";
            PreviewBodyBlock.Text = "Albums and songs appear here when the server exposes a music library.";
            AddInlineEmpty(AlbumsPanel, "Refresh to load albums.");
            AddInlineEmpty(SongsPanel, "Refresh to load songs.");
        }

        private void SetLoadingState(bool isLoading)
        {
            RefreshButton.IsEnabled = !isLoading;
            FallbackRetryButton.IsEnabled = !isLoading;
            AllSongsButton.IsEnabled = !isLoading;
        }

        private void RenderAlbums(
            EmbySession session,
            EmbyApiClient client,
            IReadOnlyList<EmbyMediaItem> albums)
        {
            AlbumsPanel.Children.Clear();
            _firstAlbumButton = null;
            AlbumsCountBlock.Text = albums.Count == 1 ? "1 album" : albums.Count + " albums";

            if (albums.Count == 0)
            {
                AddInlineEmpty(AlbumsPanel, "No albums returned.");
                return;
            }

            foreach (var album in albums)
            {
                var button = CreateMusicButton(
                    session,
                    client,
                    album,
                    "Album " + CreateItemName(album),
                    CreateAlbumSecondaryLine(album),
                    DoubleResource("TvListArtworkSize", 76));
                button.Tag = album;
                button.GotFocus += (sender, args) => UpdatePreview(album, "Album");
                button.Click += AlbumButton_OnClick;

                if (_firstAlbumButton == null)
                {
                    _firstAlbumButton = button;
                    UpdatePreview(album, "Album");
                }

                AlbumsPanel.Children.Add(button);
            }
        }

        private void RenderSongs(
            EmbySession session,
            EmbyApiClient client,
            IReadOnlyList<EmbyMediaItem> songs,
            string title,
            bool showAllSongsButton)
        {
            SongsPanel.Children.Clear();
            _firstSongButton = null;
            SongsTitleBlock.Text = title;
            SongsCountBlock.Text = songs.Count == 1 ? "1 song" : songs.Count + " songs";
            AllSongsButton.Visibility = showAllSongsButton ? Visibility.Visible : Visibility.Collapsed;

            if (songs.Count == 0)
            {
                RenderSongsEmpty("No songs returned.");
                return;
            }

            foreach (var song in songs)
            {
                var button = CreateMusicButton(
                    session,
                    client,
                    song,
                    "Song " + CreateItemName(song),
                    CreateSongSecondaryLine(song),
                    DoubleResource("TvCompactArtworkSize", 64));
                button.Tag = song;
                button.GotFocus += (sender, args) => UpdatePreview(song, "Song");
                button.Click += SongButton_OnClick;

                if (_firstSongButton == null)
                {
                    _firstSongButton = button;
                    if (_firstAlbumButton == null)
                    {
                        UpdatePreview(song, "Song");
                    }
                }

                SongsPanel.Children.Add(button);
            }
        }

        private void RenderSongsEmpty(string message)
        {
            SongsPanel.Children.Clear();
            _firstSongButton = null;
            SongsCountBlock.Text = message;
            AddInlineEmpty(SongsPanel, message);
        }

        private Button CreateMusicButton(
            EmbySession session,
            EmbyApiClient client,
            EmbyMediaItem item,
            string automationName,
            string secondaryLine,
            double artworkSize)
        {
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["TvListButtonStyle"],
                Content = CreateMusicButtonContent(session, client, item, secondaryLine, artworkSize)
            };
            AutomationProperties.SetName(button, automationName);
            return button;
        }

        private UIElement CreateMusicButtonContent(
            EmbySession session,
            EmbyApiClient client,
            EmbyMediaItem item,
            string secondaryLine,
            double artworkSize)
        {
            var grid = new Grid
            {
                ColumnSpacing = 16
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(artworkSize) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var artwork = CreateArtworkFrame(session, client, item, artworkSize);
            Grid.SetColumn(artwork, 0);
            grid.Children.Add(artwork);

            var textStack = new StackPanel
            {
                Spacing = 3,
                VerticalAlignment = VerticalAlignment.Center
            };
            textStack.Children.Add(new TextBlock
            {
                Text = CreateItemName(item),
                Style = (Style)Application.Current.Resources["TvBodyValueTextStyle"],
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            });
            textStack.Children.Add(new TextBlock
            {
                Text = secondaryLine,
                Style = (Style)Application.Current.Resources["TvMutedBodyTextStyle"],
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap
            });

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);
            return grid;
        }

        private Border CreateArtworkFrame(
            EmbySession session,
            EmbyApiClient client,
            EmbyMediaItem item,
            double artworkSize)
        {
            var frame = new Border
            {
                Width = artworkSize,
                Height = artworkSize,
                Background = BrushResource("AppRaisedSurfaceBrush"),
                BorderBrush = BrushResource("AppHairlineBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };

            var imageUri = TryCreateImageUri(session, client, item, (int)Math.Max(160, artworkSize * 3));
            if (imageUri != null)
            {
                frame.Child = new Image
                {
                    Stretch = Stretch.UniformToFill,
                    Source = new BitmapImage(imageUri)
                };
                return frame;
            }

            frame.Child = new SymbolIcon
            {
                Symbol = Symbol.MusicInfo,
                Foreground = BrushResource("AppMutedTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return frame;
        }

        private static Uri? TryCreateImageUri(
            EmbySession session,
            EmbyApiClient client,
            EmbyMediaItem item,
            int maxWidth)
        {
            if (!string.IsNullOrWhiteSpace(item.PrimaryImageTag))
            {
                var itemId = string.IsNullOrWhiteSpace(item.PrimaryImageItemId) ? item.Id : item.PrimaryImageItemId;
                return new Uri(client.GetImageUrl(session, itemId, "Primary", maxWidth));
            }

            if (!string.IsNullOrWhiteSpace(item.ThumbImageTag))
            {
                var itemId = string.IsNullOrWhiteSpace(item.ThumbImageItemId) ? item.Id : item.ThumbImageItemId;
                return new Uri(client.GetImageUrl(session, itemId, "Thumb", maxWidth));
            }

            return null;
        }

        private async void AlbumButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var album = button == null ? null : button.Tag as EmbyMediaItem;
            if (album != null)
            {
                await LoadAlbumSongsAsync(album);
            }
        }

        private void SongButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var song = button == null ? null : button.Tag as EmbyMediaItem;
            var songName = song == null ? "this song" : CreateItemName(song);
            ShowUnsupportedPanel(songName);
        }

        private void ShowUnsupportedPanel(string songName)
        {
            UnsupportedTitleBlock.Text = "Music playback unavailable";
            UnsupportedBodyBlock.Text = "Browsing works, but this build does not start audio playback yet: " + songName + ".";
            UnsupportedPanel.Visibility = Visibility.Visible;
            UnsupportedCloseButton.Focus(FocusState.Keyboard);
        }

        private void UnsupportedCloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            CloseUnsupportedPanel();
        }

        private void MusicPage_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!TransientLayerInputPolicy.ShouldDismiss(
                UnsupportedPanel.Visibility == Visibility.Visible,
                IsBackKey(e.Key)))
            {
                return;
            }

            e.Handled = true;
            CloseUnsupportedPanel();
        }

        private void CloseUnsupportedPanel()
        {
            UnsupportedPanel.Visibility = Visibility.Collapsed;
            FocusDefaultContent();
        }

        private void ShowFallback(string title, string body)
        {
            StatusBlock.Text = "Needs attention";
            FallbackTitleBlock.Text = title;
            FallbackBodyBlock.Text = body;
            FallbackPanel.Visibility = Visibility.Visible;
            FallbackRetryButton.Focus(FocusState.Keyboard);
        }

        private void UpdatePreview(EmbyMediaItem item, string badge)
        {
            PreviewBadgeBlock.Text = badge;
            PreviewTitleBlock.Text = CreateItemName(item);
            PreviewBodyBlock.Text = CreatePreviewBody(item, badge);
        }

        private void AddInlineEmpty(StackPanel panel, string text)
        {
            panel.Children.Add(new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["TvMutedBodyTextStyle"]
            });
        }

        private static string CreateMusicStatus(int albums, int songs)
        {
            return (albums == 1 ? "1 album" : albums + " albums") +
                " - " +
                (songs == 1 ? "1 song" : songs + " songs");
        }

        private static string CreateAlbumSecondaryLine(EmbyMediaItem album)
        {
            var parts = new List<string>();
            if (album.ProductionYear.HasValue && album.ProductionYear.Value > 0)
            {
                parts.Add(album.ProductionYear.Value.ToString());
            }

            if (album.ChildCount.HasValue && album.ChildCount.Value > 0)
            {
                parts.Add(album.ChildCount.Value == 1 ? "1 track" : album.ChildCount.Value + " tracks");
            }

            return parts.Count == 0 ? "Album" : string.Join(" - ", parts);
        }

        private static string CreateSongSecondaryLine(EmbyMediaItem song)
        {
            var parts = new List<string>();
            var track = CreateTrackNumber(song);
            if (!string.IsNullOrWhiteSpace(track))
            {
                parts.Add(track);
            }

            var runtime = FormatRuntime(song.RunTimeTicks);
            if (!string.IsNullOrWhiteSpace(runtime))
            {
                parts.Add(runtime);
            }

            return parts.Count == 0 ? "Song" : string.Join(" - ", parts);
        }

        private static string CreatePreviewBody(EmbyMediaItem item, string badge)
        {
            var parts = new List<string>();
            parts.Add(string.Equals(badge, "Album", StringComparison.Ordinal)
                ? CreateAlbumSecondaryLine(item)
                : CreateSongSecondaryLine(item));

            if (!string.IsNullOrWhiteSpace(item.Overview))
            {
                parts.Add(item.Overview);
            }

            return string.Join(Environment.NewLine + Environment.NewLine, parts);
        }

        private static string CreateTrackNumber(EmbyMediaItem song)
        {
            if (song.ParentIndexNumber.HasValue && song.IndexNumber.HasValue)
            {
                return "Disc " + song.ParentIndexNumber.Value + " - Track " + song.IndexNumber.Value;
            }

            return song.IndexNumber.HasValue
                ? "Track " + song.IndexNumber.Value
                : "";
        }

        private static string FormatRuntime(long? ticks)
        {
            if (!ticks.HasValue || ticks.Value <= 0)
            {
                return "";
            }

            var duration = TimeSpan.FromTicks(ticks.Value);
            return duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss")
                : duration.ToString(@"m\:ss");
        }

        private static string CreateItemName(EmbyMediaItem item)
        {
            if (item == null)
            {
                return "";
            }

            return string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
        }

        private bool CanApplyLoad(int loadGeneration)
        {
            return !_isUnloaded && loadGeneration == _loadGeneration;
        }

        private static bool IsBackKey(VirtualKey key)
        {
            return key == VirtualKey.GamepadB ||
                key == VirtualKey.Escape ||
                key == VirtualKey.GoBack;
        }

        private static Brush BrushResource(string key)
        {
            return (Brush)Application.Current.Resources[key];
        }

        private static double DoubleResource(string key, double fallback)
        {
            var value = Application.Current.Resources[key];
            return value is double ? (double)value : fallback;
        }
    }
}
