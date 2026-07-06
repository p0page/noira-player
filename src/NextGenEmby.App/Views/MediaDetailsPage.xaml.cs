using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Input;
using NextGenEmby.Core.Playback;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NextGenEmby.App.Views
{
    public sealed partial class MediaDetailsPage : Page, ITvContentFocusTarget
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private EmbyMediaItem? _item;
        private IReadOnlyList<EmbyMediaSource> _mediaSources = Array.Empty<EmbyMediaSource>();
        private string _selectedMediaSourceId = "";
        private IReadOnlyList<EmbyMediaItem> _organizeAncestors = Array.Empty<EmbyMediaItem>();
        private IReadOnlyList<EmbyMediaItem> _collectionTargets = Array.Empty<EmbyMediaItem>();
        private IReadOnlyList<EmbyMediaItem> _playlistTargets = Array.Empty<EmbyMediaItem>();
        private DetailsAddToSheetKind _activeAddToSheet = DetailsAddToSheetKind.None;
        private EmbySession? _addToSheetSession;
        private int _addToSheetPreviewIndex;
        private int _addToSheetLoadGeneration;
        private bool _addToSheetConfirming;
        private object? _addToSheetReturnFocus;
        private bool _isUnloaded;
        private int _loadGeneration;
#if DEBUG
        private const int DevelopmentDetailsFocusRetryCount = 6;
        private bool _usesDevelopmentDetailsFixture;
        private int _developmentDetailsFocusGeneration;
        private IReadOnlyDictionary<string, string> _developmentDetailsArtworkUris =
            new Dictionary<string, string>(StringComparer.Ordinal);
#endif

        public MediaDetailsPage()
        {
            InitializeComponent();
            AddHandler(KeyDownEvent, new KeyEventHandler(Page_OnKeyDown), true);
            Unloaded += MediaDetailsPage_OnUnloaded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _isUnloaded = false;
            var loadGeneration = BeginLoad();
#if DEBUG
            _usesDevelopmentDetailsFixture = false;
            _developmentDetailsArtworkUris = new Dictionary<string, string>(StringComparer.Ordinal);
#endif

            var item = e.Parameter as EmbyMediaItem;
            if (item != null)
            {
                _item = item;
                RenderItem();
                await LoadDetailsAsync(item.Id, item.Name, loadGeneration);
                return;
            }

            var request = e.Parameter as MediaDetailsNavigationRequest;
            if (request != null)
            {
#if DEBUG
                if (request.UseDevelopmentFixture)
                {
                    RenderDevelopmentDetailsFixture(loadGeneration);
                    return;
                }
#endif
                _item = new EmbyMediaItem
                {
                    Id = request.ItemId,
                    Name = request.ItemName
                };
                RenderItem();
                await LoadDetailsAsync(request.ItemId, request.ItemName, loadGeneration);
                return;
            }

            _item = null;
            RenderItem();
        }

        private void MediaDetailsPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = true;
            _loadGeneration++;
        }

        private void Page_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (TryRouteAddToSheetKey(e.Key))
            {
                e.Handled = true;
                return;
            }

            if (TryRouteDetailsDirectionalKey(e.Key, e.OriginalSource))
            {
                e.Handled = true;
                return;
            }

            if (!IsBackKey(e.Key))
            {
                return;
            }

            if (Frame != null && Frame.CanGoBack)
            {
                Frame.GoBack();
                e.Handled = true;
            }
        }

        private bool TryRouteAddToSheetKey(VirtualKey key)
        {
            if (_activeAddToSheet == DetailsAddToSheetKind.None)
            {
                return false;
            }

            switch (key)
            {
                case VirtualKey.Up:
                case VirtualKey.Left:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.GamepadLeftThumbstickUp:
                case VirtualKey.GamepadLeftThumbstickLeft:
                    MoveAddToSheetSelection(-1);
                    return true;

                case VirtualKey.Down:
                case VirtualKey.Right:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadDPadRight:
                case VirtualKey.GamepadLeftThumbstickDown:
                case VirtualKey.GamepadLeftThumbstickRight:
                    MoveAddToSheetSelection(1);
                    return true;

                case VirtualKey.Enter:
                case VirtualKey.Space:
                case VirtualKey.GamepadA:
                    _ = ConfirmAddToSheetAsync();
                    return true;

                case VirtualKey.Escape:
                case VirtualKey.GoBack:
                case VirtualKey.GamepadB:
                    CancelAddToSheet();
                    return true;

                default:
                    return false;
            }
        }

        private bool TryRouteDetailsDirectionalKey(VirtualKey key, object originalSource)
        {
            var focusedElement = FocusManager.GetFocusedElement() as DependencyObject ??
                originalSource as DependencyObject;
            if (focusedElement == null)
            {
                return false;
            }

            var focusedAction = ResolveFocusedActionButton(focusedElement);
            if (focusedAction.HasValue)
            {
                if (IsLeftKey(key) || IsRightKey(key))
                {
                    var next = MediaDetailsActionNavigationPolicy.MoveHorizontal(
                        focusedAction.Value,
                        IsRightKey(key) ? 1 : -1,
                        RestartButton.Visibility == Visibility.Visible);
                    return next.HasValue && FocusAction(next.Value, FocusState.Keyboard);
                }

                if (IsDownKey(key))
                {
                    return FocusFirstVersionButton(FocusState.Keyboard) ||
                        FocusFirstOrganizeButton(FocusState.Keyboard) ||
                        FocusFirstBelowVersions(FocusState.Keyboard);
                }
            }

            if (IsFocusWithin(focusedElement, VersionsPanel))
            {
                if (IsDownKey(key))
                {
                    return FocusNextButtonInPanelOrFallback(
                        VersionsPanel,
                        focusedElement,
                        FocusState.Keyboard,
                        () => FocusFirstBelowVersions(FocusState.Keyboard));
                }

                if (IsUpKey(key))
                {
                    return FocusPreviousButtonInPanelOrFallback(
                        VersionsPanel,
                        focusedElement,
                        FocusState.Keyboard,
                        () => FocusAction(MediaDetailsActionButton.Play, FocusState.Keyboard));
                }
            }

            if (IsFocusWithin(focusedElement, EpisodesPanel))
            {
                if (IsDownKey(key))
                {
                    return FocusNextButtonInPanelOrFallback(
                        EpisodesPanel,
                        focusedElement,
                        FocusState.Keyboard,
                        () => FocusFirstAfterEpisodes(FocusState.Keyboard));
                }

                if (IsUpKey(key))
                {
                    return FocusPreviousButtonInPanelOrFallback(
                        EpisodesPanel,
                        focusedElement,
                        FocusState.Keyboard,
                        () => FocusLastOrganizeButton(FocusState.Keyboard) ||
                            FocusLastVersionButton(FocusState.Keyboard) ||
                            FocusAction(MediaDetailsActionButton.Play, FocusState.Keyboard));
                }
            }

            if (IsFocusWithin(focusedElement, OrganizeActionsPanel))
            {
                if (IsLeftKey(key) || IsRightKey(key))
                {
                    return IsRightKey(key)
                        ? FocusNextButtonInPanelOrFallback(
                            OrganizeActionsPanel,
                            focusedElement,
                            FocusState.Keyboard,
                            () => FocusFirstButton(OrganizeActionsPanel, FocusState.Keyboard))
                        : FocusPreviousButtonInPanelOrFallback(
                            OrganizeActionsPanel,
                            focusedElement,
                            FocusState.Keyboard,
                            () => FocusLastButton(OrganizeActionsPanel, FocusState.Keyboard));
                }

                if (IsDownKey(key))
                {
                    return FocusFirstAfterOrganize(FocusState.Keyboard);
                }

                if (IsUpKey(key))
                {
                    return FocusLastVersionButton(FocusState.Keyboard) ||
                        FocusAction(MediaDetailsActionButton.Play, FocusState.Keyboard);
                }
            }

            if (IsFocusWithin(focusedElement, SimilarItemsPanel))
            {
                if (IsDownKey(key))
                {
                    return FocusFirstButton(PeoplePanel, FocusState.Keyboard);
                }

                if (IsUpKey(key))
                {
                    return FocusLastButton(EpisodesPanel, FocusState.Keyboard) ||
                        FocusLastOrganizeButton(FocusState.Keyboard) ||
                        FocusLastVersionButton(FocusState.Keyboard) ||
                        FocusAction(MediaDetailsActionButton.Play, FocusState.Keyboard);
                }
            }

            if (IsFocusWithin(focusedElement, PeoplePanel) && IsUpKey(key))
            {
                return FocusFirstButton(SimilarItemsPanel, FocusState.Keyboard) ||
                    FocusLastButton(EpisodesPanel, FocusState.Keyboard) ||
                    FocusLastOrganizeButton(FocusState.Keyboard) ||
                    FocusLastVersionButton(FocusState.Keyboard) ||
                    FocusAction(MediaDetailsActionButton.Play, FocusState.Keyboard);
            }

            return false;
        }

        private MediaDetailsActionButton? ResolveFocusedActionButton(DependencyObject element)
        {
            if (IsFocusWithin(element, PlayButton))
            {
                return MediaDetailsActionButton.Play;
            }

            if (IsFocusWithin(element, RestartButton))
            {
                return MediaDetailsActionButton.Restart;
            }

            if (IsFocusWithin(element, FavoriteButton))
            {
                return MediaDetailsActionButton.Favorite;
            }

            if (IsFocusWithin(element, WatchedButton))
            {
                return MediaDetailsActionButton.Watched;
            }

            if (IsFocusWithin(element, RefreshButton))
            {
                return MediaDetailsActionButton.Refresh;
            }

            return null;
        }

        private bool FocusAction(MediaDetailsActionButton action, FocusState focusState)
        {
            var control = GetActionControl(action);
            return control != null &&
                control.Visibility == Visibility.Visible &&
                control.IsEnabled &&
                control.Focus(focusState);
        }

        private Control? GetActionControl(MediaDetailsActionButton action)
        {
            switch (action)
            {
                case MediaDetailsActionButton.Play:
                    return PlayButton;
                case MediaDetailsActionButton.Restart:
                    return RestartButton;
                case MediaDetailsActionButton.Favorite:
                    return FavoriteButton;
                case MediaDetailsActionButton.Watched:
                    return WatchedButton;
                case MediaDetailsActionButton.Refresh:
                    return RefreshButton;
                default:
                    return null;
            }
        }

        private bool FocusFirstVersionButton(FocusState focusState)
        {
            return FocusFirstButton(VersionsPanel, focusState);
        }

        private bool FocusLastVersionButton(FocusState focusState)
        {
            return FocusLastButton(VersionsPanel, focusState);
        }

        private bool FocusFirstBelowVersions(FocusState focusState)
        {
            return FocusFirstOrganizeButton(focusState) ||
                FocusFirstAfterOrganize(focusState);
        }

        private bool FocusFirstOrganizeButton(FocusState focusState)
        {
            return FocusFirstButton(OrganizeActionsPanel, focusState);
        }

        private bool FocusLastOrganizeButton(FocusState focusState)
        {
            return FocusLastButton(OrganizeActionsPanel, focusState);
        }

        private bool FocusFirstAfterOrganize(FocusState focusState)
        {
            return FocusFirstButton(EpisodesPanel, focusState) ||
                FocusFirstAfterEpisodes(focusState);
        }

        private bool FocusFirstAfterEpisodes(FocusState focusState)
        {
            return FocusFirstButton(SimilarItemsPanel, focusState) ||
                FocusFirstButton(PeoplePanel, focusState);
        }

        private bool FocusNextButtonInPanelOrFallback(
            Panel panel,
            DependencyObject focusedElement,
            FocusState focusState,
            Func<bool> fallback)
        {
            var buttons = GetFocusableButtons(panel);
            var focusedIndex = FindFocusedButtonIndex(buttons, focusedElement);
            if (focusedIndex >= 0 && focusedIndex < buttons.Count - 1)
            {
                return buttons[focusedIndex + 1].Focus(focusState);
            }

            return fallback();
        }

        private bool FocusPreviousButtonInPanelOrFallback(
            Panel panel,
            DependencyObject focusedElement,
            FocusState focusState,
            Func<bool> fallback)
        {
            var buttons = GetFocusableButtons(panel);
            var focusedIndex = FindFocusedButtonIndex(buttons, focusedElement);
            if (focusedIndex > 0)
            {
                return buttons[focusedIndex - 1].Focus(focusState);
            }

            return fallback();
        }

        private bool FocusFirstButton(Panel panel, FocusState focusState)
        {
            var button = GetFocusableButtons(panel).FirstOrDefault();
            return button != null && button.Focus(focusState);
        }

        private bool FocusLastButton(Panel panel, FocusState focusState)
        {
            var button = GetFocusableButtons(panel).LastOrDefault();
            return button != null && button.Focus(focusState);
        }

        private List<Button> GetFocusableButtons(Panel panel)
        {
            return panel.Children
                .OfType<Button>()
                .Where(button => button.Visibility == Visibility.Visible && button.IsEnabled)
                .ToList();
        }

        private int FindFocusedButtonIndex(IReadOnlyList<Button> buttons, DependencyObject focusedElement)
        {
            for (var index = 0; index < buttons.Count; index++)
            {
                if (IsFocusWithin(focusedElement, buttons[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool IsFocusWithin(DependencyObject element, DependencyObject parent)
        {
            var current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, parent))
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
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

        private void RenderItem()
        {
            ResetPlaybackSections();
            ResetArtwork();

            if (_item == null || string.IsNullOrWhiteSpace(_item.Id))
            {
                TitleBlock.Text = "Item unavailable";
                MetaBlock.Text = "";
                OverviewBlock.Text = "";
                StatusBlock.Text = "Go back and choose another item.";
                PlayButton.IsEnabled = false;
                PlayButtonText.Text = "Play";
                RestartButton.Visibility = Visibility.Collapsed;
                FavoriteButton.IsEnabled = false;
                WatchedButton.IsEnabled = false;
                return;
            }

            TitleBlock.Text = string.IsNullOrWhiteSpace(_item.Name) ? _item.Id : _item.Name;
            MetaBlock.Text = CreateMeta(_item);
            OverviewBlock.Text = string.IsNullOrWhiteSpace(_item.Overview)
                ? "No overview available."
                : _item.Overview;
            StatusBlock.Text = "";
            UpdateActionButtons();
            RenderOrganizeSection();
            FocusDefaultContent();
        }

        private async Task LoadDetailsAsync(string itemId, string fallbackName, int loadGeneration)
        {
            if (!CanApplyLoad(loadGeneration))
            {
                return;
            }

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
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                if (session == null)
                {
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                using (var http = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(http, session);
                    var loadedItem = await client.GetItemAsync(session, itemId);
                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    _item = loadedItem;
                    RenderItem();
                    await LoadImagesAsync(client, session, loadGeneration);
                    await LoadPlaybackInfoAsync(client, session, itemId, loadGeneration);
                    await LoadOrganizeAsync(client, session, loadGeneration);
                    await LoadSeriesEpisodesAsync(client, session, loadGeneration);
                    await LoadSecondaryRailsAsync(client, session, loadGeneration);
                }
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                StatusBlock.Text = "";
            }
            catch
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                RenderItem();
                StatusBlock.Text = "Unable to load details.";
            }
        }

        private Task LoadImagesAsync(EmbyApiClient client, EmbySession session, int loadGeneration)
        {
            var item = _item;
            if (!CanApplyLoad(loadGeneration) || item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return Task.CompletedTask;
            }

            try
            {
                ResetArtwork();

                var logoArtwork = EmbyArtworkPolicy.SelectLogoArtwork(item, 900);
                if (logoArtwork != null)
                {
                    LogoImage.Source = new BitmapImage(new Uri(client.GetImageUrl(
                        session,
                        logoArtwork.ItemId,
                        logoArtwork.ImageType,
                        logoArtwork.MaxWidth)));
                    LogoImage.Visibility = Visibility.Visible;
                }

                var posterArtwork = EmbyArtworkPolicy.SelectPosterArtwork(item, 720);
                if (posterArtwork != null)
                {
                    PosterImage.Source = new BitmapImage(new Uri(client.GetImageUrl(
                        session,
                        posterArtwork.ItemId,
                        posterArtwork.ImageType,
                        posterArtwork.MaxWidth)));
                    PosterFallbackBlock.Visibility = Visibility.Collapsed;
                }

                var backdropArtwork = EmbyArtworkPolicy.SelectHeroArtwork(item, 1920);
                if (backdropArtwork != null)
                {
                    BackdropImage.Source = new BitmapImage(new Uri(client.GetImageUrl(
                        session,
                        backdropArtwork.ItemId,
                        backdropArtwork.ImageType,
                        backdropArtwork.MaxWidth)));
                }
            }
            catch
            {
                if (CanApplyLoad(loadGeneration))
                {
                    StatusBlock.Text = "Unable to load artwork.";
                }
            }

            return Task.CompletedTask;
        }

#if DEBUG
        private void RenderDevelopmentDetailsFixture(int loadGeneration)
        {
            if (!CanApplyLoad(loadGeneration))
            {
                return;
            }

            var fixture = DevelopmentDetailsFixture.Create();
            _usesDevelopmentDetailsFixture = true;
            _developmentDetailsArtworkUris = fixture.ArtworkUris;
            _item = fixture.Item;

            RenderItem();
            ApplyDevelopmentDetailsArtwork(fixture.Item);

            _mediaSources = fixture.MediaSources;
            _selectedMediaSourceId = ResolveSelectedPlaybackMediaSourceId();
            RenderPlaybackInfo();

            _organizeAncestors = fixture.OrganizeAncestors;
            _collectionTargets = fixture.CollectionTargets;
            _playlistTargets = fixture.PlaylistTargets;
            RenderOrganizeSection();
            RenderDevelopmentSecondaryRails(fixture);

            StatusBlock.Text = "Fixture details loaded.";
            FocusDevelopmentDefaultContentAsync();
        }

        private async void FocusDevelopmentDefaultContentAsync()
        {
            var focusGeneration = ++_developmentDetailsFocusGeneration;
            for (var attempt = 0; attempt < DevelopmentDetailsFocusRetryCount; attempt++)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (!_isUnloaded &&
                        _usesDevelopmentDetailsFixture &&
                        focusGeneration == _developmentDetailsFocusGeneration)
                    {
                        FocusDefaultContent();
                    }
                });

                await Task.Delay(120);
            }
        }

        private void ApplyDevelopmentDetailsArtwork(EmbyMediaItem item)
        {
            var posterSource = CreateDevelopmentArtworkImageSource(item.PrimaryImageItemId, "Primary");
            if (posterSource != null)
            {
                PosterImage.Source = posterSource;
                PosterFallbackBlock.Visibility = Visibility.Collapsed;
            }

            var backdropSource = CreateDevelopmentArtworkImageSource(item.BackdropImageItemId, "Backdrop");
            if (backdropSource != null)
            {
                BackdropImage.Source = backdropSource;
            }
        }

        private void RenderDevelopmentSecondaryRails(DevelopmentDetailsFixtureSnapshot fixture)
        {
            SimilarItemsPanel.Children.Clear();
            foreach (var item in fixture.SimilarItems.Take(12))
            {
                SimilarItemsPanel.Children.Add(CreateDevelopmentSimilarItemButton(item));
            }

            SimilarSection.Visibility = SimilarItemsPanel.Children.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            PeoplePanel.Children.Clear();
            foreach (var person in fixture.Item.People.Take(18))
            {
                PeoplePanel.Children.Add(CreateDevelopmentPersonButton(person));
            }

            PeopleSection.Visibility = PeoplePanel.Children.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private BitmapImage? CreateDevelopmentArtworkImageSource(string itemId, string imageType)
        {
            var key = DevelopmentDetailsFixture.ArtworkKey(itemId, imageType);
            if (!_developmentDetailsArtworkUris.TryGetValue(key, out var uri) ||
                string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            return new BitmapImage(new Uri(uri));
        }

        private ImageBrush? CreateDevelopmentArtworkBrush(EmbyMediaItem item, string imageType)
        {
            var itemId = ResolveArtworkItemId(item, imageType);
            var imageSource = CreateDevelopmentArtworkImageSource(itemId, imageType);
            if (imageSource == null)
            {
                return null;
            }

            return new ImageBrush
            {
                ImageSource = imageSource,
                Stretch = Stretch.UniformToFill
            };
        }

        private ImageBrush? CreateDevelopmentArtworkBrush(EmbyPerson person)
        {
            var imageSource = CreateDevelopmentArtworkImageSource(person.Id, "Primary");
            if (imageSource == null)
            {
                return null;
            }

            return new ImageBrush
            {
                ImageSource = imageSource,
                Stretch = Stretch.UniformToFill
            };
        }

        private static string ResolveArtworkItemId(EmbyMediaItem item, string imageType)
        {
            if (item == null)
            {
                return "";
            }

            if (string.Equals(imageType, "Thumb", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item.ThumbImageItemId))
            {
                return item.ThumbImageItemId;
            }

            if (string.Equals(imageType, "Backdrop", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item.BackdropImageItemId))
            {
                return item.BackdropImageItemId;
            }

            if (string.Equals(imageType, "Primary", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(item.PrimaryImageItemId))
            {
                return item.PrimaryImageItemId;
            }

            return item.Id;
        }
#endif

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

            var mediaSourceId = ResolveSelectedPlaybackMediaSourceId();
            NavigateToPlayback(mediaSourceId);
        }

        private void Restart_OnClick(object sender, RoutedEventArgs e)
        {
            if (_item == null || string.IsNullOrWhiteSpace(_item.Id) || !CanPlay(_item))
            {
                return;
            }

            var mediaSourceId = ResolveSelectedPlaybackMediaSourceId();
            NavigateToPlayback(mediaSourceId, 0);
        }

        private async void Favorite_OnClick(object sender, RoutedEventArgs e)
        {
            var item = _item;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            var current = item.UserData != null && item.UserData.IsFavorite;
#if DEBUG
            if (_usesDevelopmentDetailsFixture)
            {
                ApplyDevelopmentFixtureUserDataToggle(
                    userData => MediaDetailsUserDataTogglePolicy.ToggleFavorite(userData),
                    !current ? "Fixture favorite added." : "Fixture favorite removed.",
                    FavoriteButton);
                return;
            }
#endif
            await UpdateUserDataAsync(
                async (client, session) => await client.SetFavoriteAsync(session, item.Id, !current),
                !current ? "Added to favorites." : "Removed from favorites.",
                FavoriteButton);
        }

        private async void Watched_OnClick(object sender, RoutedEventArgs e)
        {
            var item = _item;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            var current = item.UserData != null && item.UserData.Played;
#if DEBUG
            if (_usesDevelopmentDetailsFixture)
            {
                ApplyDevelopmentFixtureUserDataToggle(
                    userData => MediaDetailsUserDataTogglePolicy.TogglePlayed(userData),
                    !current ? "Fixture marked watched." : "Fixture marked unwatched.",
                    WatchedButton);
                return;
            }
#endif
            await UpdateUserDataAsync(
                async (client, session) => await client.SetPlayedAsync(session, item.Id, !current),
                !current ? "Marked watched." : "Marked unwatched.",
                WatchedButton);
        }

        private async void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
#if DEBUG
            if (_usesDevelopmentDetailsFixture)
            {
                RenderDevelopmentDetailsFixture(BeginLoad());
                return;
            }
#endif
            var itemId = _item == null ? "" : _item.Id;
            var itemName = _item == null ? "" : _item.Name;
            await LoadDetailsAsync(itemId, itemName, BeginLoad());
        }

        private async void AddToCollection_OnClick(object sender, RoutedEventArgs e)
        {
            await OpenAddToSheetAsync(DetailsAddToSheetKind.Collection);
        }

        private async void AddToPlaylist_OnClick(object sender, RoutedEventArgs e)
        {
            await OpenAddToSheetAsync(DetailsAddToSheetKind.Playlist);
        }

        private async Task OpenAddToSheetAsync(DetailsAddToSheetKind sheetKind)
        {
            if (sheetKind == DetailsAddToSheetKind.None ||
                _item == null ||
                string.IsNullOrWhiteSpace(_item.Id))
            {
                return;
            }

#if DEBUG
            if (_usesDevelopmentDetailsFixture)
            {
                OpenDevelopmentAddToSheet(sheetKind);
                return;
            }
#endif

            var loadGeneration = ++_addToSheetLoadGeneration;
            _activeAddToSheet = sheetKind;
            _addToSheetSession = null;
            _addToSheetPreviewIndex = 0;
            _addToSheetConfirming = false;
            _addToSheetReturnFocus = FocusManager.GetFocusedElement();

            AddToSheetTitleBlock.Text = sheetKind == DetailsAddToSheetKind.Collection
                ? "Add to collection"
                : "Add to playlist";
            AddToSheetSubtitleBlock.Text = "Loading destinations...";
            AddToSheetRoot.Visibility = Visibility.Visible;
            RenderAddToSheetLoading();

            try
            {
                var session = await _sessionStore.LoadAsync();
                if (!CanApplyAddToSheetLoad(loadGeneration, sheetKind))
                {
                    return;
                }

                if (session == null)
                {
                    RenderAddToSheetMessage("Sign in first.");
                    return;
                }

                _addToSheetSession = session;
                using (var http = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(http, session);
                    var targets = await LoadAddToTargetsAsync(client, session, sheetKind);
                    if (!CanApplyAddToSheetLoad(loadGeneration, sheetKind))
                    {
                        return;
                    }

                    if (sheetKind == DetailsAddToSheetKind.Collection)
                    {
                        _collectionTargets = targets;
                    }
                    else
                    {
                        _playlistTargets = targets;
                    }
                }

                AddToSheetSubtitleBlock.Text = GetActiveAddToTargets().Count == 0
                    ? "No destinations found."
                    : "Choose a destination";
                RenderAddToSheetOptions();
                FocusAddToSheetOption(_addToSheetPreviewIndex);
            }
            catch
            {
                if (CanApplyAddToSheetLoad(loadGeneration, sheetKind))
                {
                    RenderAddToSheetMessage("Unable to load destinations.");
                }
            }
        }

#if DEBUG
        private void OpenDevelopmentAddToSheet(DetailsAddToSheetKind sheetKind)
        {
            _addToSheetLoadGeneration++;
            _activeAddToSheet = sheetKind;
            _addToSheetSession = null;
            _addToSheetPreviewIndex = 0;
            _addToSheetConfirming = false;
            _addToSheetReturnFocus = FocusManager.GetFocusedElement();

            AddToSheetTitleBlock.Text = sheetKind == DetailsAddToSheetKind.Collection
                ? "Add to collection"
                : "Add to playlist";
            AddToSheetRoot.Visibility = Visibility.Visible;
            AddToSheetSubtitleBlock.Text = GetActiveAddToTargets().Count == 0
                ? "No fixture destinations found."
                : "Choose a fixture destination";
            RenderAddToSheetOptions();
            FocusAddToSheetOption(_addToSheetPreviewIndex);
        }

        private void ConfirmDevelopmentAddToSheet()
        {
            var item = _item;
            var targets = GetActiveAddToTargets();
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || targets.Count == 0)
            {
                CloseAddToSheet(restoreFocus: true);
                return;
            }

            var target = targets[Math.Max(0, Math.Min(_addToSheetPreviewIndex, targets.Count - 1))];
            if (string.IsNullOrWhiteSpace(target.Id))
            {
                CloseAddToSheet(restoreFocus: true);
                return;
            }

            AddAncestorIfMissing(target);
            RenderOrganizeSection();
            StatusBlock.Text = _activeAddToSheet == DetailsAddToSheetKind.Collection
                ? "Added to fixture collection: " + CreateDisplayName(target)
                : "Added to fixture playlist: " + CreateDisplayName(target);
            CloseAddToSheet(restoreFocus: true);
        }
#endif

        private static async Task<IReadOnlyList<EmbyMediaItem>> LoadAddToTargetsAsync(
            EmbyApiClient client,
            EmbySession session,
            DetailsAddToSheetKind sheetKind)
        {
            var includeItemTypes = sheetKind == DetailsAddToSheetKind.Collection ? "BoxSet" : "Playlist";
            var collectionType = sheetKind == DetailsAddToSheetKind.Collection ? "boxsets" : "playlists";
            var targets = await client.GetItemsAsync(session, new EmbyItemsQuery
            {
                IncludeItemTypes = includeItemTypes,
                CollectionTypes = collectionType,
                SortBy = "SortName",
                SortOrder = "Ascending",
                Limit = 100,
                Recursive = true
            });

            if (targets.Count == 0)
            {
                targets = await client.GetItemsAsync(session, new EmbyItemsQuery
                {
                    IncludeItemTypes = includeItemTypes,
                    SortBy = "SortName",
                    SortOrder = "Ascending",
                    Limit = 100,
                    Recursive = true
                });
            }

            return targets
                .Where(target => sheetKind == DetailsAddToSheetKind.Collection
                    ? IsCollectionItem(target)
                    : IsPlaylistItem(target))
                .Where(target => !string.IsNullOrWhiteSpace(target.Id))
                .Take(80)
                .ToList();
        }

        private void RenderAddToSheetLoading()
        {
            AddToSheetOptionsPanel.Children.Clear();
            AddToSheetOptionsPanel.Children.Add(CreateAddToSheetMessageButton("Loading..."));
            FocusAddToSheetOption(0);
        }

        private void RenderAddToSheetMessage(string message)
        {
            AddToSheetSubtitleBlock.Text = message;
            AddToSheetOptionsPanel.Children.Clear();
            AddToSheetOptionsPanel.Children.Add(CreateAddToSheetMessageButton(message));
            FocusAddToSheetOption(0);
        }

        private void RenderAddToSheetOptions()
        {
            AddToSheetOptionsPanel.Children.Clear();
            var targets = GetActiveAddToTargets();
            if (targets.Count == 0)
            {
                AddToSheetOptionsPanel.Children.Add(CreateAddToSheetMessageButton("No destinations found."));
                _addToSheetPreviewIndex = 0;
                return;
            }

            _addToSheetPreviewIndex = Math.Max(0, Math.Min(_addToSheetPreviewIndex, targets.Count - 1));
            for (var i = 0; i < targets.Count; i++)
            {
                AddToSheetOptionsPanel.Children.Add(CreateAddToTargetButton(i, targets[i]));
            }
        }

        private void MoveAddToSheetSelection(int offset)
        {
            var targets = GetActiveAddToTargets();
            if (targets.Count == 0)
            {
                FocusAddToSheetOption(0);
                return;
            }

            var nextIndex = LibraryOptionSheetPolicy.MovePreviewIndex(
                _addToSheetPreviewIndex,
                targets.Count,
                offset);

            if (nextIndex == _addToSheetPreviewIndex)
            {
                FocusAddToSheetOption(_addToSheetPreviewIndex);
                return;
            }

            _addToSheetPreviewIndex = nextIndex;
            RenderAddToSheetOptions();
            FocusAddToSheetOption(_addToSheetPreviewIndex);
        }

        private async Task ConfirmAddToSheetAsync()
        {
            if (_activeAddToSheet == DetailsAddToSheetKind.None || _addToSheetConfirming)
            {
                return;
            }

#if DEBUG
            if (_usesDevelopmentDetailsFixture)
            {
                ConfirmDevelopmentAddToSheet();
                return;
            }
#endif

            var item = _item;
            var targets = GetActiveAddToTargets();
            if (item == null || string.IsNullOrWhiteSpace(item.Id) || targets.Count == 0)
            {
                CloseAddToSheet(restoreFocus: true);
                return;
            }

            var target = targets[Math.Max(0, Math.Min(_addToSheetPreviewIndex, targets.Count - 1))];
            if (string.IsNullOrWhiteSpace(target.Id))
            {
                CloseAddToSheet(restoreFocus: true);
                return;
            }

            _addToSheetConfirming = true;
            SetAddToSheetOptionsEnabled(false);
            AddToSheetSubtitleBlock.Text = "Adding...";
            try
            {
                var session = await _sessionStore.LoadAsync();
                if (session == null)
                {
                    AddToSheetSubtitleBlock.Text = "Sign in first.";
                    return;
                }

                using (var http = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(http, session);
                    if (_activeAddToSheet == DetailsAddToSheetKind.Collection)
                    {
                        await client.AddItemToCollectionAsync(session, target.Id, item.Id);
                    }
                    else
                    {
                        await client.AddItemToPlaylistAsync(session, target.Id, item.Id);
                    }
                }

                AddAncestorIfMissing(target);
                RenderOrganizeSection();
                StatusBlock.Text = _activeAddToSheet == DetailsAddToSheetKind.Collection
                    ? "Added to collection: " + CreateDisplayName(target)
                    : "Added to playlist: " + CreateDisplayName(target);
                CloseAddToSheet(restoreFocus: true);
            }
            catch
            {
                AddToSheetSubtitleBlock.Text = "Unable to add. Try another destination.";
                SetAddToSheetOptionsEnabled(true);
                FocusAddToSheetOption(_addToSheetPreviewIndex);
            }
            finally
            {
                _addToSheetConfirming = false;
            }
        }

        private void CancelAddToSheet()
        {
            if (_activeAddToSheet == DetailsAddToSheetKind.None)
            {
                return;
            }

            CloseAddToSheet(restoreFocus: true);
        }

        private void CloseAddToSheet(bool restoreFocus)
        {
            if (AddToSheetRoot == null)
            {
                return;
            }

            var returnTarget = _addToSheetReturnFocus as Control;
            _activeAddToSheet = DetailsAddToSheetKind.None;
            _addToSheetSession = null;
            _addToSheetLoadGeneration++;
            _addToSheetConfirming = false;
            _addToSheetReturnFocus = null;
            AddToSheetOptionsPanel.Children.Clear();
            AddToSheetRoot.Visibility = Visibility.Collapsed;

            if (restoreFocus && returnTarget != null)
            {
                returnTarget.Focus(FocusState.Keyboard);
            }
        }

        private bool CanApplyAddToSheetLoad(int loadGeneration, DetailsAddToSheetKind sheetKind)
        {
            return !_isUnloaded &&
                loadGeneration == _addToSheetLoadGeneration &&
                _activeAddToSheet == sheetKind;
        }

        private IReadOnlyList<EmbyMediaItem> GetActiveAddToTargets()
        {
            return _activeAddToSheet == DetailsAddToSheetKind.Collection
                ? _collectionTargets
                : _playlistTargets;
        }

        private async Task LoadPlaybackInfoAsync(
            EmbyApiClient client,
            EmbySession session,
            string itemId,
            int loadGeneration)
        {
            if (!CanApplyLoad(loadGeneration))
            {
                return;
            }

            try
            {
                var mediaSources = await client.GetPlaybackInfoAsync(session, itemId);
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                _mediaSources = mediaSources;
                _selectedMediaSourceId = ResolveSelectedPlaybackMediaSourceId();
                RenderPlaybackInfo();
            }
            catch
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                VersionsPanel.Children.Clear();
                AudioSummaryBlock.Text = CanPlay(_item) ? "Audio: unavailable" : "";
                SubtitleSummaryBlock.Text = CanPlay(_item) ? "Subtitles: unavailable" : "";
            }
        }

        private async Task LoadSeriesEpisodesAsync(EmbyApiClient client, EmbySession session, int loadGeneration)
        {
            if (!CanApplyLoad(loadGeneration))
            {
                return;
            }

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
                var seasons = await LoadSeriesSeasonsWithFallbackAsync(client, session, item.Id);
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                var loadedAnyEpisodes = false;
                EpisodesPanel.Children.Clear();
                foreach (var season in seasons.Where(season => !string.IsNullOrWhiteSpace(season.Id)).Take(12))
                {
                    AddSeasonHeader(season);
                    var episodes = await LoadSeriesEpisodesWithFallbackAsync(client, session, item.Id, season.Id);
                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    foreach (var episode in episodes.Take(40))
                    {
                        loadedAnyEpisodes = true;
                        AddEpisodeButton(episode);
                    }
                }

                if (!loadedAnyEpisodes)
                {
                    AddEpisodeMessage("No episodes found.");
                }

                if (!CanPlay(item))
                {
                    FocusDefaultContent();
                }
            }
            catch
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                EpisodesPanel.Children.Clear();
                AddEpisodeMessage("Unable to load episodes.");
                if (!CanPlay(item))
                {
                    FocusDefaultContent();
                }
            }
        }

        private static async Task<IReadOnlyList<EmbyMediaItem>> LoadSeriesSeasonsWithFallbackAsync(
            EmbyApiClient client,
            EmbySession session,
            string seriesId)
        {
            try
            {
                var seasons = await client.GetSeriesSeasonsAsync(session, seriesId);
                if (seasons.Count > 0)
                {
                    return seasons;
                }
            }
            catch
            {
            }

            return await client.GetChildrenAsync(session, seriesId, "Season");
        }

        private static async Task<IReadOnlyList<EmbyMediaItem>> LoadSeriesEpisodesWithFallbackAsync(
            EmbyApiClient client,
            EmbySession session,
            string seriesId,
            string seasonId)
        {
            try
            {
                var episodes = await client.GetSeriesEpisodesAsync(session, seriesId, seasonId, 40);
                if (episodes.Count > 0)
                {
                    return episodes;
                }
            }
            catch
            {
            }

            return await client.GetChildrenAsync(session, seasonId, "Episode");
        }

        private async Task LoadOrganizeAsync(EmbyApiClient client, EmbySession session, int loadGeneration)
        {
            if (!CanApplyLoad(loadGeneration))
            {
                return;
            }

            var item = _item;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                _organizeAncestors = Array.Empty<EmbyMediaItem>();
                RenderOrganizeSection();
                return;
            }

            OrganizeSummaryBlock.Text = "Loading library links...";
            try
            {
                var ancestors = await client.GetItemAncestorsAsync(session, item.Id);
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                _organizeAncestors = ancestors;
                RenderOrganizeSection();
            }
            catch
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                _organizeAncestors = Array.Empty<EmbyMediaItem>();
                RenderOrganizeSection("Library links unavailable.");
            }
        }

        private void RenderOrganizeSection(string statusPrefix = "")
        {
            var item = _item;
            var hasItem = item != null && !string.IsNullOrWhiteSpace(item.Id);
            OrganizeSection.Visibility = hasItem ? Visibility.Visible : Visibility.Collapsed;
            AddToCollectionButton.IsEnabled = hasItem;
            AddToPlaylistButton.IsEnabled = hasItem;

            if (!hasItem)
            {
                OrganizeSummaryBlock.Text = "";
                return;
            }

            var collections = _organizeAncestors
                .Where(IsCollectionItem)
                .Select(CreateDisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Take(3)
                .ToList();
            var playlists = _organizeAncestors
                .Where(IsPlaylistItem)
                .Select(CreateDisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Take(3)
                .ToList();

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(statusPrefix))
            {
                parts.Add(statusPrefix);
            }

            if (collections.Count > 0)
            {
                parts.Add("Collections: " + string.Join(", ", collections));
            }

            if (playlists.Count > 0)
            {
                parts.Add("Playlists: " + string.Join(", ", playlists));
            }

            if (parts.Count == 0)
            {
                parts.Add("Not linked to a collection or playlist yet.");
            }

            OrganizeSummaryBlock.Text = string.Join("  /  ", parts);
        }

        private async Task LoadSecondaryRailsAsync(EmbyApiClient client, EmbySession session, int loadGeneration)
        {
            if (!CanApplyLoad(loadGeneration))
            {
                return;
            }

            RenderPeopleRail(session, client);
            await LoadSimilarItemsAsync(client, session, loadGeneration);
        }

        private async Task LoadSimilarItemsAsync(EmbyApiClient client, EmbySession session, int loadGeneration)
        {
            SimilarItemsPanel.Children.Clear();
            SimilarSection.Visibility = Visibility.Collapsed;
            var item = _item;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            try
            {
                var similarItems = await client.GetSimilarItemsAsync(session, item.Id, 18);
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                foreach (var similarItem in similarItems
                    .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Id) && candidate.Id != item.Id)
                    .Take(12))
                {
                    SimilarItemsPanel.Children.Add(CreateSimilarItemButton(session, client, similarItem));
                }

                SimilarSection.Visibility = SimilarItemsPanel.Children.Count == 0
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            catch
            {
                if (CanApplyLoad(loadGeneration))
                {
                    SimilarItemsPanel.Children.Clear();
                    SimilarSection.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void RenderPeopleRail(EmbySession session, EmbyApiClient client)
        {
            PeoplePanel.Children.Clear();
            PeopleSection.Visibility = Visibility.Collapsed;
            var item = _item;
            if (item == null || item.People == null || item.People.Count == 0)
            {
                return;
            }

            foreach (var person in item.People
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name))
                .Take(18))
            {
                PeoplePanel.Children.Add(CreatePersonButton(session, client, person));
            }

            PeopleSection.Visibility = PeoplePanel.Children.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;
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
                FontSize = 22,
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
                VersionsPanel.Children.Add(CreateSourceButton(source));
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
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.WrapWholeWords
            });
            text.Children.Add(new TextBlock
            {
                Text = CreateMeta(episode),
                FontSize = 14,
                Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AppMutedTextBrush"],
                TextWrapping = TextWrapping.Wrap
            });

            button.Content = text;
            button.Click += Episode_OnClick;
            EpisodesPanel.Children.Add(button);
        }

#if DEBUG
        private Button CreateDevelopmentSimilarItemButton(EmbyMediaItem item)
        {
            var button = new Button
            {
                Width = 148,
                Height = 220,
                MinWidth = 148,
                MinHeight = 220,
                Padding = new Thickness(0),
                Tag = item,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true
            };
            AutomationProperties.SetName(button, string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name);
            button.Click += SimilarItem_OnClick;
            button.GotFocus += SecondaryRailButton_OnGotFocus;

            var root = new Grid
            {
                Background = (Brush)Application.Current.Resources["AppRaisedSurfaceBrush"]
            };

            var artworkBrush = CreateDevelopmentArtworkBrush(item, "Primary");
            if (artworkBrush != null)
            {
                root.Background = artworkBrush;
            }

            root.Children.Add(CreateRailCardBorder());
            root.Children.Add(CreateSecondaryRailTextScrim(
                string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
                CreateMeta(item)));

            button.Content = root;
            return button;
        }

        private Button CreateDevelopmentPersonButton(EmbyPerson person)
        {
            var button = new Button
            {
                Width = 154,
                Height = 154,
                MinWidth = 154,
                MinHeight = 154,
                Padding = new Thickness(0),
                Tag = person,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true,
                IsEnabled = !string.IsNullOrWhiteSpace(person.Id)
            };
            AutomationProperties.SetName(button, CreatePersonAutomationName(person));
            button.Click += Person_OnClick;
            button.GotFocus += SecondaryRailButton_OnGotFocus;

            var root = new Grid
            {
                Background = (Brush)Application.Current.Resources["AppRaisedSurfaceBrush"]
            };

            var artworkBrush = CreateDevelopmentArtworkBrush(person);
            if (artworkBrush != null)
            {
                root.Background = artworkBrush;
            }

            root.Children.Add(CreateRailCardBorder());
            root.Children.Add(CreateSecondaryRailTextScrim(person.Name, CreatePersonRole(person)));

            button.Content = root;
            return button;
        }

        private static Border CreateSecondaryRailTextScrim(string title, string meta)
        {
            return new Border
            {
                Background = (Brush)Application.Current.Resources["AppCardScrimBrush"],
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(10, 8, 10, 8),
                Child = new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title ?? "",
                            FontSize = 15,
                            FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 2
                        },
                        new TextBlock
                        {
                            Text = meta ?? "",
                            FontSize = 12,
                            Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"],
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 1
                        }
                    }
                }
            };
        }
#endif

        private Button CreateSimilarItemButton(EmbySession session, EmbyApiClient client, EmbyMediaItem item)
        {
            var button = new Button
            {
                Width = 148,
                Height = 220,
                MinWidth = 148,
                MinHeight = 220,
                Padding = new Thickness(0),
                Tag = item,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true
            };
            AutomationProperties.SetName(button, string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name);
            button.Click += SimilarItem_OnClick;
            button.GotFocus += SecondaryRailButton_OnGotFocus;

            var root = new Grid
            {
                Background = (Brush)Application.Current.Resources["AppRaisedSurfaceBrush"]
            };

            var posterArtwork = EmbyArtworkPolicy.SelectPosterArtwork(item, 360);
            if (posterArtwork != null)
            {
                root.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(client.GetImageUrl(
                        session,
                        posterArtwork.ItemId,
                        posterArtwork.ImageType,
                        posterArtwork.MaxWidth))),
                    Stretch = Stretch.UniformToFill
                };
            }

            root.Children.Add(CreateRailCardBorder());
            root.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["AppCardScrimBrush"],
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(10, 8, 10, 8),
                Child = new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
                            FontSize = 15,
                            FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 2
                        },
                        new TextBlock
                        {
                            Text = CreateMeta(item),
                            FontSize = 12,
                            Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"],
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 1
                        }
                    }
                }
            });

            button.Content = root;
            return button;
        }

        private Button CreatePersonButton(EmbySession session, EmbyApiClient client, EmbyPerson person)
        {
            var button = new Button
            {
                Width = 154,
                Height = 154,
                MinWidth = 154,
                MinHeight = 154,
                Padding = new Thickness(0),
                Tag = person,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true,
                IsEnabled = !string.IsNullOrWhiteSpace(person.Id)
            };
            AutomationProperties.SetName(button, CreatePersonAutomationName(person));
            button.Click += Person_OnClick;
            button.GotFocus += SecondaryRailButton_OnGotFocus;

            var root = new Grid
            {
                Background = (Brush)Application.Current.Resources["AppRaisedSurfaceBrush"]
            };

            if (!string.IsNullOrWhiteSpace(person.Id) && !string.IsNullOrWhiteSpace(person.PrimaryImageTag))
            {
                root.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(client.GetImageUrl(session, person.Id, "Primary", 280))),
                    Stretch = Stretch.UniformToFill
                };
            }

            root.Children.Add(CreateRailCardBorder());
            root.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["AppCardScrimBrush"],
                VerticalAlignment = VerticalAlignment.Bottom,
                Padding = new Thickness(10, 8, 10, 8),
                Child = new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = person.Name,
                            FontSize = 15,
                            FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 2
                        },
                        new TextBlock
                        {
                            Text = CreatePersonRole(person),
                            FontSize = 12,
                            Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"],
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxLines = 1
                        }
                    }
                }
            });

            button.Content = root;
            return button;
        }

        private Button CreateAddToSheetMessageButton(string message)
        {
            var button = new Button
            {
                Tag = -1,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = 58,
                Background = BrushResource("AppChromeBrush"),
                BorderBrush = BrushResource("AppHairlineBrush"),
                UseSystemFocusVisuals = true
            };
            button.Click += AddToSheetOption_OnClick;

            button.Content = new TextBlock
            {
                Text = message ?? "",
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = BrushResource("AppTextBrush"),
                TextWrapping = TextWrapping.WrapWholeWords
            };

            return button;
        }

        private Button CreateAddToTargetButton(int index, EmbyMediaItem target)
        {
            var isPreview = index == _addToSheetPreviewIndex;
            var button = new Button
            {
                Tag = index,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                MinHeight = 84,
                Padding = new Thickness(10),
                Background = isPreview ? BrushResource("AppRaisedSurfaceBrush") : BrushResource("AppChromeBrush"),
                BorderBrush = isPreview ? BrushResource("AppAccentBrush") : BrushResource("AppHairlineBrush"),
                UseSystemFocusVisuals = true
            };
            AutomationProperties.SetName(button, CreateDisplayName(target));
            button.Click += AddToSheetOption_OnClick;
            button.GotFocus += AddToSheetOption_OnGotFocus;

            var row = new Grid
            {
                ColumnSpacing = 14
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var artworkFrame = CreateAddToTargetArtwork(target);
            Grid.SetColumn(artworkFrame, 0);
            row.Children.Add(artworkFrame);

            var text = new StackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center
            };
            text.Children.Add(new TextBlock
            {
                Text = CreateDisplayName(target),
                FontSize = 18,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = BrushResource("AppTextBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            });
            text.Children.Add(new TextBlock
            {
                Text = CreateAddToTargetMeta(target),
                FontSize = 14,
                Foreground = BrushResource("AppMutedTextBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1
            });
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            var selectedIcon = new SymbolIcon(Symbol.Accept)
            {
                Foreground = BrushResource("AppAccentBrush"),
                Visibility = isPreview ? Visibility.Visible : Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(selectedIcon, 2);
            row.Children.Add(selectedIcon);

            button.Content = row;
            return button;
        }

        private FrameworkElement CreateAddToTargetArtwork(EmbyMediaItem target)
        {
            var frame = new Border
            {
                Width = 112,
                Height = 64,
                CornerRadius = new CornerRadius(6),
                Background = BrushResource("AppRaisedSurfaceBrush")
            };

            var artwork = EmbyArtworkPolicy.SelectItemWideArtwork(target, 360);
#if DEBUG
            var developmentArtworkBrush = CreateDevelopmentArtworkBrush(target, "Thumb");
            if (developmentArtworkBrush != null)
            {
                frame.Background = developmentArtworkBrush;
                return frame;
            }
#endif
            if (artwork != null && _addToSheetSession != null)
            {
                frame.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(BuildImageUrl(_addToSheetSession, artwork))),
                    Stretch = Stretch.UniformToFill
                };
                return frame;
            }

            frame.Child = new TextBlock
            {
                Text = CreateFallbackInitial(target),
                FontSize = 22,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = BrushResource("AppMutedTextBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            return frame;
        }

        private static Border CreateRailCardBorder()
        {
            return new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["AppHairlineBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            };
        }

        private void AddSeasonHeader(EmbyMediaItem season)
        {
            EpisodesPanel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(season.Name) ? "Season" : season.Name,
                FontSize = 19,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.WrapWholeWords
            });
        }

        private void AddEpisodeMessage(string message)
        {
            EpisodesPanel.Children.Add(CreateMutedText(message));
        }

        public bool FocusDefaultContent()
        {
            var target = MediaDetailsDefaultFocusPolicy.Decide(
                PlayButton.IsEnabled,
                CountEpisodeButtons());

            switch (target)
            {
                case MediaDetailsDefaultFocusTarget.Play:
                    return PlayButton.Focus(FocusState.Programmatic);
                case MediaDetailsDefaultFocusTarget.FirstEpisode:
                    return FocusFirstEpisodeButton(FocusState.Programmatic);
                default:
                    return RefreshButton.Focus(FocusState.Programmatic);
            }
        }

        private bool FocusFirstEpisodeButton(FocusState focusState)
        {
            foreach (var child in EpisodesPanel.Children)
            {
                var button = child as Button;
                if (button != null && button.Focus(focusState))
                {
                    return true;
                }
            }

            return false;
        }

        private int CountEpisodeButtons()
        {
            var count = 0;
            foreach (var child in EpisodesPanel.Children)
            {
                if (child is Button)
                {
                    count++;
                }
            }

            return count;
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
                    new PlaybackLaunchRequest(
                        episode.Id,
                        episode.Name,
                        startPositionTicks,
                        runtimeTicks: episode.RunTimeTicks.GetValueOrDefault()));
                return;
            }

            Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(episode.Id, episode.Name));
        }

        private void SimilarItem_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button == null ? null : button.Tag as EmbyMediaItem;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            Frame.Navigate(
                typeof(MediaDetailsPage),
                new MediaDetailsNavigationRequest(item.Id, string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name));
        }

        private void Person_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var person = button == null ? null : button.Tag as EmbyPerson;
            if (person == null || string.IsNullOrWhiteSpace(person.Id))
            {
                return;
            }

            Frame.Navigate(
                typeof(LibraryPage),
                new LibraryNavigationRequest(
                    person.Name,
                    "",
                    "Movie,Series,Episode,Video,MusicVideo",
                    "",
                    "",
                    new LibraryNavigationQuery(personIds: person.Id)));
        }

        private static void SecondaryRailButton_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var target = sender as Control;
            if (target == null)
            {
                return;
            }

            target.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true,
                HorizontalAlignmentRatio = 0.12,
                HorizontalOffset = -18,
                VerticalAlignmentRatio = 0.62,
                VerticalOffset = -12
            });
        }

        private static void AddToSheetOption_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var target = sender as Control;
            if (target == null)
            {
                return;
            }

            target.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true,
                VerticalAlignmentRatio = 0.5,
                VerticalOffset = -8
            });
        }

        private void AddToSheetOption_OnClick(object sender, RoutedEventArgs e)
        {
            if (_activeAddToSheet == DetailsAddToSheetKind.None)
            {
                return;
            }

            var button = sender as Button;
            if (button != null && button.Tag is int index && index >= 0)
            {
                _addToSheetPreviewIndex = index;
                RenderAddToSheetOptions();
            }

            _ = ConfirmAddToSheetAsync();
        }

        private void FocusAddToSheetOption(int index)
        {
            if (index < 0 || index >= AddToSheetOptionsPanel.Children.Count)
            {
                return;
            }

            var control = AddToSheetOptionsPanel.Children[index] as Control;
            if (control != null)
            {
                control.Focus(FocusState.Keyboard);
            }
        }

        private void SetAddToSheetOptionsEnabled(bool isEnabled)
        {
            foreach (var button in AddToSheetOptionsPanel.Children.OfType<Button>())
            {
                button.IsEnabled = isEnabled;
            }
        }

        private void AddAncestorIfMissing(EmbyMediaItem target)
        {
            if (string.IsNullOrWhiteSpace(target.Id) ||
                _organizeAncestors.Any(ancestor => string.Equals(ancestor.Id, target.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var ancestors = _organizeAncestors.ToList();
            ancestors.Add(target);
            _organizeAncestors = ancestors;
        }

        private void SourceVersion_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var source = button?.Tag as EmbyMediaSource;
            if (source == null || string.IsNullOrWhiteSpace(source.Id))
            {
                return;
            }

            var decision = MediaDetailsVersionSelectionPolicy.Select(
                GetMediaSourceIds(),
                source.Id,
                CreateSourceSummary(source),
                _selectedMediaSourceId);
            _selectedMediaSourceId = decision.SelectedMediaSourceId;
            StatusBlock.Text = decision.StatusMessage;
            UpdateSourceButtonStates();
        }

        private void NavigateToPlayback(string mediaSourceId)
        {
            var startPositionTicks = _item == null || _item.UserData == null
                ? 0
                : _item.UserData.PlaybackPositionTicks;
            NavigateToPlayback(mediaSourceId, startPositionTicks);
        }

        private void NavigateToPlayback(string mediaSourceId, long startPositionTicks)
        {
            if (_item == null || string.IsNullOrWhiteSpace(_item.Id) || !CanPlay(_item))
            {
                return;
            }

            Frame.Navigate(
                typeof(PlaybackPage),
                new PlaybackLaunchRequest(
                    _item.Id,
                    _item.Name,
                    startPositionTicks,
                    mediaSourceId ?? "",
                    _item.RunTimeTicks.GetValueOrDefault()));
        }

        private async Task UpdateUserDataAsync(
            Func<EmbyApiClient, EmbySession, Task<EmbyUserData>> mutation,
            string successMessage,
            Button restoreFocusButton)
        {
            var item = _item;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            SetUserDataButtonsEnabled(false);
            StatusBlock.Text = "Updating item...";
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
                    var userData = await mutation(client, session);
                    ApplyUserData(userData);
                }

                UpdateActionButtons();
                StatusBlock.Text = successMessage;
                restoreFocusButton.Focus(FocusState.Programmatic);
            }
            catch
            {
                StatusBlock.Text = "Unable to update item status.";
                restoreFocusButton.Focus(FocusState.Programmatic);
            }
            finally
            {
                SetUserDataButtonsEnabled(true);
            }
        }

        private void ApplyUserData(EmbyUserData userData)
        {
            if (_item == null)
            {
                return;
            }

            _item.UserData = userData ?? new EmbyUserData();
        }

#if DEBUG
        private void ApplyDevelopmentFixtureUserDataToggle(
            Func<EmbyUserData, EmbyUserData> mutation,
            string successMessage,
            Button restoreFocusButton)
        {
            var item = _item;
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            var current = item.UserData ?? new EmbyUserData();
            ApplyUserData(mutation(current));
            UpdateActionButtons();
            StatusBlock.Text = successMessage;
            restoreFocusButton.Focus(FocusState.Programmatic);
        }
#endif

        private void SetUserDataButtonsEnabled(bool isEnabled)
        {
            FavoriteButton.IsEnabled = isEnabled && _item != null && !string.IsNullOrWhiteSpace(_item.Id);
            WatchedButton.IsEnabled = isEnabled && _item != null && !string.IsNullOrWhiteSpace(_item.Id);
        }

        private void ResetPlaybackSections()
        {
            _mediaSources = Array.Empty<EmbyMediaSource>();
            _selectedMediaSourceId = "";
            _organizeAncestors = Array.Empty<EmbyMediaItem>();
            VersionsPanel.Children.Clear();
            AudioSummaryBlock.Text = "";
            SubtitleSummaryBlock.Text = "";
            OrganizeSummaryBlock.Text = "";
            OrganizeSection.Visibility = Visibility.Collapsed;
            AddToCollectionButton.IsEnabled = false;
            AddToPlaylistButton.IsEnabled = false;
            EpisodesPanel.Children.Clear();
            EpisodesSection.Visibility = Visibility.Collapsed;
            SimilarItemsPanel.Children.Clear();
            SimilarSection.Visibility = Visibility.Collapsed;
            PeoplePanel.Children.Clear();
            PeopleSection.Visibility = Visibility.Collapsed;
            CloseAddToSheet(restoreFocus: false);
        }

        private void ResetArtwork()
        {
            LogoImage.Source = null;
            LogoImage.Visibility = Visibility.Collapsed;
            TitleBlock.Visibility = Visibility.Visible;
            PosterImage.Source = null;
            BackdropImage.Source = null;
            PosterFallbackBlock.Visibility = Visibility.Visible;
        }

        private void LogoImage_OnImageOpened(object sender, RoutedEventArgs e)
        {
            if (LogoImage.Source != null)
            {
                TitleBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void LogoImage_OnImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            LogoImage.Source = null;
            LogoImage.Visibility = Visibility.Collapsed;
            TitleBlock.Visibility = Visibility.Visible;
        }

        private int BeginLoad()
        {
            _loadGeneration++;
            return _loadGeneration;
        }

        private bool CanApplyLoad(int loadGeneration)
        {
            return !_isUnloaded && loadGeneration == _loadGeneration;
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
                FontSize = 18,
                Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AppMutedTextBrush"],
                TextWrapping = TextWrapping.Wrap
            };
        }

        private Button CreateSourceButton(EmbyMediaSource source)
        {
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(18, 14, 18, 14),
                Tag = source,
                UseSystemFocusVisuals = true
            };

            var layout = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var marker = new Border
            {
                Name = "SourceSelectionMarker",
                Width = 4,
                Margin = new Thickness(0, 2, 14, 2),
                Background = (Brush)Application.Current.Resources["AppWarmBrush"],
                CornerRadius = new CornerRadius(2),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(marker, 0);

            var panel = new StackPanel
            {
                Spacing = 4
            };
            Grid.SetColumn(panel, 1);

            panel.Children.Add(new TextBlock
            {
                Text = CreateSourceSummary(source),
                FontSize = 18,
                TextWrapping = TextWrapping.WrapWholeWords
            });

            var details = CreateSourceDetails(source);
            if (!string.IsNullOrWhiteSpace(details))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = details,
                    FontSize = 14,
                    Foreground = (Windows.UI.Xaml.Media.Brush)Application.Current.Resources["AppMutedTextBrush"],
                    TextWrapping = TextWrapping.Wrap
                });
            }

            layout.Children.Add(marker);
            layout.Children.Add(panel);

            button.Content = layout;
            ApplySourceButtonState(button, source);
            button.Click += SourceVersion_OnClick;
            return button;
        }

        private void UpdateSourceButtonStates()
        {
            foreach (var button in VersionsPanel.Children.OfType<Button>())
            {
                if (button.Tag is EmbyMediaSource source)
                {
                    ApplySourceButtonState(button, source);
                }
            }
        }

        private void ApplySourceButtonState(Button button, EmbyMediaSource source)
        {
            var isSelected = string.Equals(
                source.Id,
                _selectedMediaSourceId,
                StringComparison.Ordinal);
            button.BorderBrush = (Brush)Application.Current.Resources["AppHairlineBrush"];
            button.BorderThickness = new Thickness(1);
            button.Background = (Brush)Application.Current.Resources[
                isSelected ? "AppChromePressedBrush" : "AppChromeBrush"];

            var marker = GetSourceSelectionMarker(button);
            if (marker != null)
            {
                marker.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
            }

            var namePrefix = isSelected ? "Selected version, " : "Version, ";
            AutomationProperties.SetName(button, namePrefix + CreateSourceSummary(source));
        }

        private static Border? GetSourceSelectionMarker(Button button)
        {
            if (button.Content is Grid layout)
            {
                return layout.Children
                    .OfType<Border>()
                    .FirstOrDefault(candidate => string.Equals(
                        candidate.Name,
                        "SourceSelectionMarker",
                        StringComparison.Ordinal));
            }

            return null;
        }

        private string ResolveSelectedPlaybackMediaSourceId()
        {
            _selectedMediaSourceId = MediaDetailsVersionSelectionPolicy.ResolvePlaybackSource(
                GetMediaSourceIds(),
                _selectedMediaSourceId);
            return _selectedMediaSourceId;
        }

        private IReadOnlyList<string> GetMediaSourceIds()
        {
            return _mediaSources
                .Select(source => source.Id ?? "")
                .ToList();
        }

        private void UpdateActionButtons()
        {
            var item = _item;
            var userData = item == null ? new EmbyUserData() : item.UserData ?? new EmbyUserData();
            var canPlay = CanPlay(item);
            var actionState = MediaDetailsActionPolicy.Decide(
                canPlay,
                userData.IsFavorite,
                userData.Played,
                userData.PlaybackPositionTicks);

            PlayButton.IsEnabled = canPlay;
            PlayButtonText.Text = actionState.PlayLabel;
            RestartButton.Visibility = actionState.ShowRestart ? Visibility.Visible : Visibility.Collapsed;
            RestartButton.IsEnabled = actionState.ShowRestart;
            FavoriteButton.IsEnabled = item != null && !string.IsNullOrWhiteSpace(item.Id);
            WatchedButton.IsEnabled = item != null && !string.IsNullOrWhiteSpace(item.Id);
            FavoriteButtonText.Text = actionState.FavoriteLabel;
            WatchedButtonText.Text = actionState.WatchedLabel;
            AutomationProperties.SetName(FavoriteButton, actionState.FavoriteLabel);
            AutomationProperties.SetName(WatchedButton, actionState.WatchedLabel);
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

            if (source.HdrProfile.Kind != HdrPlaybackKind.Sdr)
            {
                parts.Add(source.HdrProfile.PlaybackStrategy);
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

        private static string CreateSourceDetails(EmbyMediaSource source)
        {
            var details = new List<string>();
            var video = source.VideoStreams.FirstOrDefault();
            if (video != null && !string.IsNullOrWhiteSpace(video.Codec))
            {
                details.Add(video.Codec.ToUpperInvariant());
            }

            if (source.HdrProfile.Kind == HdrPlaybackKind.DolbyVisionUnsupported)
            {
                details.Add("Dolby Vision unsupported");
            }

            var audioCount = source.AudioStreams.Count();
            if (audioCount > 0)
            {
                details.Add("Audio " + audioCount);
            }

            var subtitleCount = source.SubtitleStreams.Count();
            if (subtitleCount > 0)
            {
                details.Add("Subtitles " + subtitleCount);
            }

            return string.Join(" / ", details);
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

        private static string CreatePersonRole(EmbyPerson person)
        {
            if (!string.IsNullOrWhiteSpace(person.Role))
            {
                return person.Role;
            }

            return string.IsNullOrWhiteSpace(person.Type) ? "Person" : person.Type;
        }

        private static string CreatePersonAutomationName(EmbyPerson person)
        {
            var role = CreatePersonRole(person);
            return string.IsNullOrWhiteSpace(role)
                ? person.Name
                : person.Name + ", " + role;
        }

        private static bool IsCollectionItem(EmbyMediaItem item)
        {
            return item != null && string.Equals(item.Type, "BoxSet", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlaylistItem(EmbyMediaItem item)
        {
            return item != null && string.Equals(item.Type, "Playlist", StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateDisplayName(EmbyMediaItem item)
        {
            if (item == null)
            {
                return "";
            }

            return string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
        }

        private static string CreateAddToTargetMeta(EmbyMediaItem target)
        {
            var parts = new List<string>();
            parts.Add(IsCollectionItem(target) ? "Collection" : "Playlist");

            if (target.ChildCount.HasValue && target.ChildCount.Value > 0)
            {
                parts.Add(target.ChildCount.Value + " items");
            }

            if (target.ProductionYear.HasValue)
            {
                parts.Add(target.ProductionYear.Value.ToString());
            }

            return string.Join(" / ", parts);
        }

        private static string CreateFallbackInitial(EmbyMediaItem item)
        {
            var name = CreateDisplayName(item);
            return string.IsNullOrWhiteSpace(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
        }

        private static Brush BrushResource(string key)
        {
            return (Brush)Application.Current.Resources[key];
        }

        private static string BuildImageUrl(EmbySession session, EmbyImageCandidate image)
        {
            return
                $"{session.ServerUrl.TrimEnd('/')}/Items/{Uri.EscapeDataString(image.ItemId)}/Images/{Uri.EscapeDataString(image.ImageType)}" +
                $"?maxWidth={image.MaxWidth}&quality=90&api_key={Uri.EscapeDataString(session.AccessToken)}";
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

    internal enum DetailsAddToSheetKind
    {
        None,
        Collection,
        Playlist
    }
}
