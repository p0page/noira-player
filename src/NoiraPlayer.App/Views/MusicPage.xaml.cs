using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Services;
using NoiraPlayer.App.Storage;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Input;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NoiraPlayer.App.Views
{
    public sealed partial class MusicPage : Page, ITvContentFocusTarget
    {
        private const string AllMusicArtistId = "__all_music__";
        private const string SyntheticArtistIdPrefix = "artist-name:";
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private readonly List<Button> _artistButtons = new List<Button>();
        private readonly List<Button> _albumButtons = new List<Button>();
        private readonly List<Button> _songButtons = new List<Button>();
        private Button? _activeArtistButton;
        private Button? _firstArtistButton;
        private Button? _firstAlbumButton;
        private Button? _firstSongButton;
        private Button? _unsupportedReturnFocusTarget;
        private IReadOnlyList<EmbyMediaItem> _loadedAlbums = Array.Empty<EmbyMediaItem>();
        private IReadOnlyList<EmbyMediaItem> _loadedSongs = Array.Empty<EmbyMediaItem>();
        private MusicNavigationRequest? _request;
        private readonly Dictionary<string, string> _musicArtworkUris =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private int _loadGeneration;
        private bool _isUnloaded;
        public MusicPage()
        {
            InitializeComponent();
            MatteButtonFocusVisuals.PrepareCommandButton(AllSongsButton);
            MatteButtonFocusVisuals.PrepareCommandButton(FallbackRetryButton);
            MatteButtonFocusVisuals.PrepareCommandButton(UnsupportedCloseButton);
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

            if (_firstArtistButton != null && _firstArtistButton.Focus(FocusState.Keyboard))
            {
                return true;
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
                    _loadedAlbums = albums;
                    _loadedSongs = songs;
                    RenderArtists(session, client, CreateArtistItems(albums, songs), albums.Count, songs.Count);
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
            ArtistsPanel.Children.Clear();
            AlbumsPanel.Children.Clear();
            SongsPanel.Children.Clear();
            _artistButtons.Clear();
            _albumButtons.Clear();
            _songButtons.Clear();
            _activeArtistButton = null;
            _firstArtistButton = null;
            _firstAlbumButton = null;
            _firstSongButton = null;
            _unsupportedReturnFocusTarget = null;
            _loadedAlbums = Array.Empty<EmbyMediaItem>();
            _loadedSongs = Array.Empty<EmbyMediaItem>();
            _musicArtworkUris.Clear();
            ArtistsCountBlock.Text = "Loading";
            AlbumsCountBlock.Text = "Loading";
            SongsTitleBlock.Text = "Songs";
            SongsCountBlock.Text = "Loading";
            AllSongsButton.Visibility = Visibility.Collapsed;
            ClearPreviewArtwork();
            PreviewBadgeBlock.Text = "Now";
            PreviewTitleBlock.Text = "Select music";
            PreviewBodyBlock.Text = "Albums and songs appear here when the server exposes a music library.";
            StatusBlock.Text = "Loading music";
        }

        private void PrepareBrowseOnlyPreview()
        {
            FallbackPanel.Visibility = Visibility.Collapsed;
            UnsupportedPanel.Visibility = Visibility.Collapsed;
            ArtistsPanel.Children.Clear();
            AlbumsPanel.Children.Clear();
            SongsPanel.Children.Clear();
            _artistButtons.Clear();
            _albumButtons.Clear();
            _songButtons.Clear();
            _activeArtistButton = null;
            _firstArtistButton = null;
            _firstAlbumButton = null;
            _firstSongButton = null;
            _unsupportedReturnFocusTarget = null;
            _loadedAlbums = Array.Empty<EmbyMediaItem>();
            _loadedSongs = Array.Empty<EmbyMediaItem>();
            _musicArtworkUris.Clear();
            ArtistsCountBlock.Text = "Refresh to load";
            AlbumsCountBlock.Text = "Refresh to load";
            SongsTitleBlock.Text = "Songs";
            SongsCountBlock.Text = "Refresh to load";
            AllSongsButton.Visibility = Visibility.Collapsed;
            ClearPreviewArtwork();
            PreviewBadgeBlock.Text = "Now";
            PreviewTitleBlock.Text = "Select music";
            PreviewBodyBlock.Text = "Albums and songs appear here when the server exposes a music library.";
            AddInlineEmpty(ArtistsPanel, "Refresh to load artists.");
            AddInlineEmpty(AlbumsPanel, "Refresh to load albums.");
            AddInlineEmpty(SongsPanel, "Refresh to load songs.");
        }

        private void SetLoadingState(bool isLoading)
        {
            RefreshButton.IsEnabled = !isLoading;
            FallbackRetryButton.IsEnabled = !isLoading;
            AllSongsButton.IsEnabled = !isLoading;
        }

        private void RenderArtists(
            EmbySession session,
            EmbyApiClient client,
            IReadOnlyList<EmbyMediaItem> artists,
            int albumCount,
            int songCount)
        {
            ArtistsPanel.Children.Clear();
            _artistButtons.Clear();
            _firstArtistButton = null;
            ArtistsCountBlock.Text = artists.Count == 1 ? "1 artist" : artists.Count + " artists";

            var browseItems = new List<EmbyMediaItem> { CreateAllMusicItem(albumCount, songCount) };
            browseItems.AddRange(artists);

            foreach (var artist in browseItems)
            {
                var button = CreateMusicButton(
                    session,
                    client,
                    artist,
                    "Artist " + CreateItemName(artist),
                    CreateArtistSecondaryLine(artist),
                    DoubleResource("TvCompactArtworkSize", 56));
                button.Tag = artist;
                button.GotFocus += (sender, args) =>
                {
                    _activeArtistButton = button;
                    UpdatePreview(artist, IsAllMusicArtist(artist) ? "Music" : "Artist");
                };
                button.Click += ArtistButton_OnClick;

                if (_firstArtistButton == null)
                {
                    _firstArtistButton = button;
                    _activeArtistButton = button;
                    UpdatePreview(artist, "Music");
                }

                ArtistsPanel.Children.Add(button);
                _artistButtons.Add(button);
            }
        }

        private void RenderAlbums(
            EmbySession session,
            EmbyApiClient client,
            IReadOnlyList<EmbyMediaItem> albums)
        {
            AlbumsPanel.Children.Clear();
            _albumButtons.Clear();
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
                _albumButtons.Add(button);
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
            _songButtons.Clear();
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
                _songButtons.Add(button);
            }
        }

        private void RenderSongsEmpty(string message)
        {
            SongsPanel.Children.Clear();
            _songButtons.Clear();
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
            CacheMusicArtworkUri(session, client, item);
            var button = new Button
            {
                Style = (Style)Application.Current.Resources["TvListButtonStyle"],
                Content = CreateMusicButtonContent(
                    CreateArtworkFrame(session, client, item, artworkSize),
                    item,
                    secondaryLine,
                    artworkSize)
            };
            MatteButtonFocusVisuals.PrepareListButton(button);
            AutomationProperties.SetName(button, automationName);
            return button;
        }

        private UIElement CreateMusicButtonContent(
            FrameworkElement artwork,
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

        private void CacheMusicArtworkUri(
            EmbySession session,
            EmbyApiClient client,
            EmbyMediaItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            var imageUri = TryCreateImageUri(session, client, item, 440);
            if (imageUri != null)
            {
                _musicArtworkUris[item.Id] = imageUri.AbsoluteUri;
            }
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

        private async void ArtistButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var artist = button == null ? null : button.Tag as EmbyMediaItem;
            if (artist == null)
            {
                return;
            }

            _activeArtistButton = button;
            await RenderArtistMusicAsync(artist);
        }

        private async Task RenderArtistMusicAsync(EmbyMediaItem artist)
        {
            var session = await _sessionStore.LoadAsync();
            if (session == null)
            {
                ShowFallback("Sign in first", "Open Settings or restart the app after signing in.");
                return;
            }

            using (var httpClient = new HttpClient())
            {
                var client = EmbyClientFactory.Create(httpClient, session);
                var albums = FilterByArtist(_loadedAlbums, artist);
                var songs = FilterByArtist(_loadedSongs, artist);
                var title = IsAllMusicArtist(artist) ? "Songs" : CreateItemName(artist);
                RenderAlbums(session, client, albums);
                RenderSongs(session, client, songs, title, showAllSongsButton: !IsAllMusicArtist(artist));
                StatusBlock.Text = IsAllMusicArtist(artist)
                    ? CreateMusicStatus(albums.Count, songs.Count)
                    : CreateArtistStatus(artist, albums.Count, songs.Count);
                FocusFirstMusicResult();
            }
        }

        private void FocusFirstMusicResult()
        {
            if (_firstAlbumButton != null && _firstAlbumButton.Focus(FocusState.Keyboard))
            {
                return;
            }

            if (_firstSongButton != null && _firstSongButton.Focus(FocusState.Keyboard))
            {
                return;
            }

            if (AllSongsButton.Visibility == Visibility.Visible &&
                AllSongsButton.Focus(FocusState.Keyboard))
            {
                return;
            }

            FocusDefaultContent();
        }

        private void SongButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            _unsupportedReturnFocusTarget = sender as Button;
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
            if (TransientLayerInputPolicy.ShouldDismiss(
                UnsupportedPanel.Visibility == Visibility.Visible,
                IsBackKey(e.Key)))
            {
                e.Handled = true;
                CloseUnsupportedPanel();
                return;
            }

            if (UnsupportedPanel.Visibility == Visibility.Visible)
            {
                return;
            }

            if ((IsRightKey(e.Key) || IsLeftKey(e.Key) || IsDownKey(e.Key) || IsUpKey(e.Key)) &&
                TryMoveWithinMusicLists(e.Key))
            {
                e.Handled = true;
            }
        }

        private bool TryMoveWithinMusicLists(VirtualKey key)
        {
            var focusedButton = FocusManager.GetFocusedElement() as Button;
            if (focusedButton == null)
            {
                return false;
            }

            if (focusedButton == AllSongsButton)
            {
                if (IsDownKey(key) && _firstSongButton != null)
                {
                    return _firstSongButton.Focus(FocusState.Keyboard);
                }

                if (IsLeftKey(key) && _firstAlbumButton != null)
                {
                    return _firstAlbumButton.Focus(FocusState.Keyboard);
                }

                if (IsLeftKey(key))
                {
                    return FocusActiveArtistOrFirst();
                }

                return false;
            }

            if (IsRightKey(key) && _artistButtons.Contains(focusedButton))
            {
                if (_firstAlbumButton != null)
                {
                    return _firstAlbumButton.Focus(FocusState.Keyboard);
                }

                return _firstSongButton != null &&
                    _firstSongButton.Focus(FocusState.Keyboard);
            }

            if (IsRightKey(key) && _albumButtons.Contains(focusedButton) && _firstSongButton != null)
            {
                return _firstSongButton.Focus(FocusState.Keyboard);
            }

            if (IsLeftKey(key) && _albumButtons.Contains(focusedButton))
            {
                return FocusActiveArtistOrFirst();
            }

            if (IsLeftKey(key) && _songButtons.Contains(focusedButton))
            {
                if (_firstAlbumButton != null)
                {
                    return _firstAlbumButton.Focus(FocusState.Keyboard);
                }

                return FocusActiveArtistOrFirst();
            }

            if (IsUpKey(key) && _songButtons.IndexOf(focusedButton) == 0 && AllSongsButton.Visibility == Visibility.Visible)
            {
                return AllSongsButton.Focus(FocusState.Keyboard);
            }

            return TryMoveWithinButtonList(_artistButtons, focusedButton, key) ||
                TryMoveWithinButtonList(_albumButtons, focusedButton, key) ||
                TryMoveWithinButtonList(_songButtons, focusedButton, key);
        }

        private static bool TryMoveWithinButtonList(
            IReadOnlyList<Button> buttons,
            Button focusedButton,
            VirtualKey key)
        {
            var currentIndex = IndexOfButton(buttons, focusedButton);
            var targetIndex = MusicListFocusPolicy.GetVerticalTargetIndex(
                currentIndex,
                buttons.Count,
                IsDownKey(key),
                IsUpKey(key));
            return targetIndex.HasValue &&
                buttons[targetIndex.Value].Focus(FocusState.Keyboard);
        }

        private static int IndexOfButton(IReadOnlyList<Button> buttons, Button focusedButton)
        {
            for (var index = 0; index < buttons.Count; index++)
            {
                if (buttons[index] == focusedButton)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool FocusActiveArtistOrFirst()
        {
            if (_activeArtistButton != null &&
                _activeArtistButton.Focus(FocusState.Keyboard))
            {
                return true;
            }

            return _firstArtistButton != null &&
                _firstArtistButton.Focus(FocusState.Keyboard);
        }

        private void CloseUnsupportedPanel()
        {
            UnsupportedPanel.Visibility = Visibility.Collapsed;
            if (FocusUnsupportedReturnTarget())
            {
                return;
            }

            FocusDefaultContent();
        }

        private bool FocusUnsupportedReturnTarget()
        {
            return _unsupportedReturnFocusTarget != null &&
                _unsupportedReturnFocusTarget.Focus(FocusState.Keyboard);
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
            UpdatePreviewArtwork(item);
            PreviewBadgeBlock.Text = badge;
            PreviewTitleBlock.Text = CreateItemName(item);
            PreviewBodyBlock.Text = CreatePreviewBody(item, badge);
        }

        private void UpdatePreviewArtwork(EmbyMediaItem item)
        {
            var source = CreatePreviewArtworkImageSource(item);
            if (source == null)
            {
                ClearPreviewArtwork();
                return;
            }

            PreviewArtworkImage.Source = source;
            PreviewArtworkFrame.Visibility = Visibility.Visible;
        }

        private void ClearPreviewArtwork()
        {
            PreviewArtworkImage.Source = null;
            PreviewArtworkFrame.Visibility = Visibility.Collapsed;
        }

        private BitmapImage? CreatePreviewArtworkImageSource(EmbyMediaItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                return null;
            }

            return _musicArtworkUris.TryGetValue(item.Id, out var uri) &&
                !string.IsNullOrWhiteSpace(uri)
                ? new BitmapImage(new Uri(uri))
                : null;
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

        private static IReadOnlyList<EmbyMediaItem> CreateArtistItems(
            IReadOnlyList<EmbyMediaItem> albums,
            IReadOnlyList<EmbyMediaItem> songs)
        {
            var artists = new List<EmbyMediaItem>();
            var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in albums.Concat(songs))
            {
                AddArtistReferences(artists, knownKeys, item.AlbumArtists);
                AddArtistReferences(artists, knownKeys, item.ArtistItems);
                AddArtistName(artists, knownKeys, "", item.AlbumArtist);

                foreach (var artistName in item.Artists)
                {
                    AddArtistName(artists, knownKeys, "", artistName);
                }
            }

            return artists
                .OrderBy(artist => CreateItemName(artist), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddArtistReferences(
            ICollection<EmbyMediaItem> artists,
            ISet<string> knownKeys,
            IReadOnlyList<EmbyItemReference> references)
        {
            foreach (var reference in references)
            {
                AddArtistName(artists, knownKeys, reference.Id, reference.Name);
            }
        }

        private static void AddArtistName(
            ICollection<EmbyMediaItem> artists,
            ISet<string> knownKeys,
            string id,
            string name)
        {
            id = id ?? "";
            name = name ?? "";
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(name) ? id.Trim() : name.Trim();
            var key = string.IsNullOrWhiteSpace(id)
                ? "name:" + displayName
                : "id:" + id.Trim();
            if (!knownKeys.Add(key))
            {
                return;
            }

            artists.Add(new EmbyMediaItem
            {
                Id = string.IsNullOrWhiteSpace(id) ? SyntheticArtistIdPrefix + displayName : id.Trim(),
                Name = displayName,
                Type = "MusicArtist",
                Overview = "Browse albums and songs by " + displayName + "."
            });
        }

        private static EmbyMediaItem CreateAllMusicItem(int albumCount, int songCount)
        {
            return new EmbyMediaItem
            {
                Id = AllMusicArtistId,
                Name = "All music",
                Type = "MusicArtist",
                Overview = CreateMusicStatus(albumCount, songCount)
            };
        }

        private static IReadOnlyList<EmbyMediaItem> FilterByArtist(
            IReadOnlyList<EmbyMediaItem> items,
            EmbyMediaItem artist)
        {
            if (IsAllMusicArtist(artist))
            {
                return items;
            }

            return items
                .Where(item => ItemMatchesArtist(item, artist))
                .ToList();
        }

        private static bool ItemMatchesArtist(EmbyMediaItem item, EmbyMediaItem artist)
        {
            if (item == null || artist == null)
            {
                return false;
            }

            if (IsAllMusicArtist(artist))
            {
                return true;
            }

            foreach (var reference in item.AlbumArtists.Concat(item.ArtistItems))
            {
                if (ArtistReferenceMatches(reference, artist))
                {
                    return true;
                }
            }

            if (ArtistNameMatches(item.AlbumArtist, artist))
            {
                return true;
            }

            return item.Artists.Any(name => ArtistNameMatches(name, artist));
        }

        private static bool ArtistReferenceMatches(EmbyItemReference reference, EmbyMediaItem artist)
        {
            if (!IsSyntheticArtist(artist) &&
                !string.IsNullOrWhiteSpace(reference.Id) &&
                string.Equals(reference.Id, artist.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return ArtistNameMatches(reference.Name, artist);
        }

        private static bool ArtistNameMatches(string name, EmbyMediaItem artist)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                string.Equals(name.Trim(), CreateItemName(artist), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllMusicArtist(EmbyMediaItem artist)
        {
            return artist != null &&
                string.Equals(artist.Id, AllMusicArtistId, StringComparison.Ordinal);
        }

        private static bool IsSyntheticArtist(EmbyMediaItem artist)
        {
            return artist == null ||
                string.IsNullOrWhiteSpace(artist.Id) ||
                artist.Id.StartsWith(SyntheticArtistIdPrefix, StringComparison.Ordinal);
        }

        private static string CreateArtistStatus(EmbyMediaItem artist, int albums, int songs)
        {
            return CreateItemName(artist) + " - " + CreateMusicStatus(albums, songs);
        }

        private static string CreateArtistSecondaryLine(EmbyMediaItem artist)
        {
            if (IsAllMusicArtist(artist))
            {
                return string.IsNullOrWhiteSpace(artist.Overview) ? "All albums and songs" : artist.Overview;
            }

            return artist.ChildCount.HasValue && artist.ChildCount.Value > 0
                ? (artist.ChildCount.Value == 1 ? "1 release" : artist.ChildCount.Value + " releases")
                : "Artist";
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
            var secondaryLine = string.Equals(badge, "Album", StringComparison.Ordinal)
                ? CreateAlbumSecondaryLine(item)
                : string.Equals(badge, "Artist", StringComparison.Ordinal) ||
                    string.Equals(badge, "Music", StringComparison.Ordinal)
                        ? CreateArtistSecondaryLine(item)
                        : CreateSongSecondaryLine(item);
            parts.Add(secondaryLine);

            if (!string.IsNullOrWhiteSpace(item.Overview) &&
                !string.Equals(item.Overview, secondaryLine, StringComparison.Ordinal))
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

        private static bool IsDownKey(VirtualKey key)
        {
            return key == VirtualKey.Down ||
                key == VirtualKey.GamepadDPadDown ||
                key == VirtualKey.GamepadLeftThumbstickDown;
        }

        private static bool IsUpKey(VirtualKey key)
        {
            return key == VirtualKey.Up ||
                key == VirtualKey.GamepadDPadUp ||
                key == VirtualKey.GamepadLeftThumbstickUp;
        }

        private static bool IsLeftKey(VirtualKey key)
        {
            return key == VirtualKey.Left ||
                key == VirtualKey.GamepadDPadLeft ||
                key == VirtualKey.GamepadLeftThumbstickLeft;
        }

        private static bool IsRightKey(VirtualKey key)
        {
            return key == VirtualKey.Right ||
                key == VirtualKey.GamepadDPadRight ||
                key == VirtualKey.GamepadLeftThumbstickRight;
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
