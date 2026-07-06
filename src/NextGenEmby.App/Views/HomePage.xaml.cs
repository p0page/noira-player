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
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.System;

namespace NextGenEmby.App.Views
{
    public sealed partial class HomePage : Page, ITvContentFocusTarget
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private EmbySession? _session;
        private EmbyApiClient? _client;
        private EmbyMediaItem? _heroItem;
        private bool _isUnloaded;
        private bool _keyHandlerAttached;
        private Control? _lastHomeFocusTarget;
        private readonly List<Button> _libraryButtons = new List<Button>();
        private readonly List<Button> _rowFirstButtons = new List<Button>();
        private readonly List<Button?> _rowMoreButtons = new List<Button?>();
        private readonly Dictionary<Button, int> _rowButtonIndexes = new Dictionary<Button, int>();
        private readonly Dictionary<Button, int> _rowMoreButtonIndexes = new Dictionary<Button, int>();
        private int _loadGeneration;
        private bool _hasRenderedHomeContent;
        private bool _isLoadingHome;
#if DEBUG
        private HomeDevelopmentFixtureNavigationRequest? _developmentFixtureRequest;
        private IReadOnlyDictionary<string, string> _developmentArtworkUris =
            new Dictionary<string, string>(StringComparer.Ordinal);
#endif

        public HomePage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Required;
            Loaded += HomePage_OnLoaded;
            Unloaded += HomePage_OnUnloaded;
            HeroPlayButton.GotFocus += HomeFocusTarget_OnGotFocus;
            HeroDetailsButton.GotFocus += HomeFocusTarget_OnGotFocus;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
#if DEBUG
            var nextFixtureRequest = e.Parameter as HomeDevelopmentFixtureNavigationRequest;
            if (_developmentFixtureRequest != nextFixtureRequest)
            {
                _hasRenderedHomeContent = false;
                _lastHomeFocusTarget = null;
            }

            _developmentFixtureRequest = nextFixtureRequest;
            if (_developmentFixtureRequest == null)
            {
                _developmentArtworkUris = new Dictionary<string, string>(StringComparer.Ordinal);
            }
#endif
        }

        private async void HomePage_OnLoaded(object sender, RoutedEventArgs e)
        {
            _isUnloaded = false;
            AttachHomeKeyHandler();

            var decision = HomeLoadPolicy.ForPageLoaded(_hasRenderedHomeContent, _isLoadingHome);
            if (decision.ShouldLoad)
            {
                await LoadHomeAsync(decision);
            }
            else if (decision.ShouldRestoreContentFocus)
            {
                await RestoreContentFocusAfterLoadedAsync();
            }
        }

        private void HomePage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachHomeKeyHandler();
            _isUnloaded = true;
            _loadGeneration++;
        }

        private async void Refresh_OnClick(object sender, RoutedEventArgs e)
        {
            await LoadHomeAsync(HomeLoadPolicy.ForRefreshRequested(_hasRenderedHomeContent));
        }

        private void MoviesLibrary_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LibraryPage), CreateLibraryRequest("Movies", "movies", "", ""));
        }

        private void TvLibrary_OnClick(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LibraryPage), CreateLibraryRequest("TV", "tvshows", "", ""));
        }

        public bool FocusDefaultContent()
        {
            if (TryMoveFocus(_lastHomeFocusTarget))
            {
                return true;
            }

            return FocusDailyStart(FocusState.Keyboard);
        }

        private async Task RestoreContentFocusAfterLoadedAsync()
        {
            await Task.Delay(50);
            if (!_isUnloaded)
            {
                FocusDefaultContent();
            }
        }

        private void HomeFocusTarget_OnGotFocus(object sender, RoutedEventArgs e)
        {
            var target = sender as Control;
            _lastHomeFocusTarget = target;
            KeepFocusedHomeTargetVisible(target);
        }

        private static void KeepFocusedHomeTargetVisible(Control? target)
        {
            if (target == null)
            {
                return;
            }

            target.StartBringIntoView(new BringIntoViewOptions
            {
                AnimationDesired = true,
                HorizontalAlignmentRatio = 0.08,
                HorizontalOffset = -18,
                VerticalAlignmentRatio = 0.38,
                VerticalOffset = -8
            });
        }

        private void HeroPlay_OnClick(object sender, RoutedEventArgs e)
        {
            var item = _heroItem;
            if (item == null || !CanPlay(item))
            {
                return;
            }

            var itemName = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            var startTicks = item.UserData == null ? 0 : item.UserData.PlaybackPositionTicks;
            Frame.Navigate(
                typeof(PlaybackPage),
                new PlaybackLaunchRequest(
                    item.Id,
                    itemName,
                    startTicks,
                    runtimeTicks: item.RunTimeTicks.GetValueOrDefault()));
        }

        private void HeroDetails_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateToDetails(_heroItem);
        }

        private void Page_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = TryRouteHomeDirectionalKey(e.Key) || e.Handled;
        }

        private void HomePage_OnCoreWindowKeyDown(CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            args.Handled = TryRouteHomeDirectionalKey(args.VirtualKey) || args.Handled;
        }

        private void AttachHomeKeyHandler()
        {
            if (_keyHandlerAttached)
            {
                return;
            }

            Window.Current.CoreWindow.KeyDown += HomePage_OnCoreWindowKeyDown;
            _keyHandlerAttached = true;
        }

        private void DetachHomeKeyHandler()
        {
            if (!_keyHandlerAttached)
            {
                return;
            }

            Window.Current.CoreWindow.KeyDown -= HomePage_OnCoreWindowKeyDown;
            _keyHandlerAttached = false;
        }

        private bool TryRouteHomeDirectionalKey(VirtualKey key)
        {
            var direction = MapHomeFocusDirection(key);
            if (!direction.HasValue)
            {
                return false;
            }

            var focusedElement = FocusManager.GetFocusedElement();
            var focusedTarget = ResolveHomeFocusTarget(focusedElement) ?? _lastHomeFocusTarget;
            var current = CreateHomeFocusTarget(focusedTarget);
            var next = HomeFocusInputPolicy.Move(
                current,
                direction.Value,
                _libraryButtons.Count,
                _rowFirstButtons.Count,
                CreateRowMoreAvailability());

            return TryMoveFocus(ResolveHomeFocusControl(next));
        }

        private static HomeFocusDirection? MapHomeFocusDirection(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Down:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadLeftThumbstickDown:
                    return HomeFocusDirection.Down;

                case VirtualKey.Up:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadLeftThumbstickUp:
                    return HomeFocusDirection.Up;

                case VirtualKey.Right:
                case VirtualKey.GamepadDPadRight:
                case VirtualKey.GamepadLeftThumbstickRight:
                    return HomeFocusDirection.Right;

                case VirtualKey.Left:
                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.GamepadLeftThumbstickLeft:
                    return HomeFocusDirection.Left;

                default:
                    return null;
            }
        }

        private bool TryMoveFocus(Control? target)
        {
            if (target == null)
            {
                return false;
            }

            if (!target.IsEnabled)
            {
                return false;
            }

            if (!target.Focus(FocusState.Keyboard))
            {
                return false;
            }

            _lastHomeFocusTarget = target;
            return true;
        }

        private Control? ResolveHomeFocusTarget(object focusedElement)
        {
            if (IsFocusWithin(focusedElement, HeroPlayButton))
            {
                return HeroPlayButton;
            }

            if (IsFocusWithin(focusedElement, HeroDetailsButton))
            {
                return HeroDetailsButton;
            }

            foreach (var libraryButton in _libraryButtons)
            {
                if (IsFocusWithin(focusedElement, libraryButton))
                {
                    return libraryButton;
                }
            }

            foreach (var rowButton in _rowButtonIndexes.Keys)
            {
                if (IsFocusWithin(focusedElement, rowButton))
                {
                    return rowButton;
                }
            }

            foreach (var moreButton in _rowMoreButtonIndexes.Keys)
            {
                if (IsFocusWithin(focusedElement, moreButton))
                {
                    return moreButton;
                }
            }

            return null;
        }

        private HomeFocusTarget? CreateHomeFocusTarget(Control? control)
        {
            if (control == HeroPlayButton)
            {
                return new HomeFocusTarget(HomeFocusZone.HeroPlay, 0);
            }

            if (control == HeroDetailsButton)
            {
                return new HomeFocusTarget(HomeFocusZone.HeroDetails, 0);
            }

            var libraryIndex = GetLibraryButtonIndex(control);
            if (libraryIndex >= 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Library, libraryIndex);
            }

            var rowIndex = GetRowButtonIndex(control);
            if (rowIndex >= 0)
            {
                return new HomeFocusTarget(HomeFocusZone.Row, rowIndex);
            }

            var rowMoreIndex = GetRowMoreButtonIndex(control);
            if (rowMoreIndex >= 0)
            {
                return new HomeFocusTarget(HomeFocusZone.RowMore, rowMoreIndex);
            }

            return null;
        }

        private Control? ResolveHomeFocusControl(HomeFocusTarget? target)
        {
            if (target == null)
            {
                return null;
            }

            switch (target.Zone)
            {
                case HomeFocusZone.HeroPlay:
                    return HeroPlayButton;
                case HomeFocusZone.HeroDetails:
                    return HeroDetailsButton;
                case HomeFocusZone.Library:
                    return target.Index >= 0 && target.Index < _libraryButtons.Count
                        ? _libraryButtons[target.Index]
                        : null;
                case HomeFocusZone.Row:
                    return target.Index >= 0 && target.Index < _rowFirstButtons.Count
                        ? _rowFirstButtons[target.Index]
                        : null;
                case HomeFocusZone.RowMore:
                    return target.Index >= 0 && target.Index < _rowMoreButtons.Count
                        ? _rowMoreButtons[target.Index]
                        : null;
                default:
                    return null;
            }
        }

        private Button? GetFirstLibraryButton()
        {
            return _libraryButtons.Count == 0 ? null : _libraryButtons[0];
        }

        private int GetLibraryButtonIndex(Control? control)
        {
            if (control == null)
            {
                return -1;
            }

            for (var index = 0; index < _libraryButtons.Count; index++)
            {
                if (_libraryButtons[index] == control)
                {
                    return index;
                }
            }

            return -1;
        }

        private int GetRowButtonIndex(Control? control)
        {
            var button = control as Button;
            if (button == null)
            {
                return -1;
            }

            int rowIndex;
            return _rowButtonIndexes.TryGetValue(button, out rowIndex) ? rowIndex : -1;
        }

        private int GetRowMoreButtonIndex(Control? control)
        {
            var button = control as Button;
            if (button == null)
            {
                return -1;
            }

            int rowIndex;
            return _rowMoreButtonIndexes.TryGetValue(button, out rowIndex) ? rowIndex : -1;
        }

        private bool[] CreateRowMoreAvailability()
        {
            var rowHasMore = new bool[_rowFirstButtons.Count];
            for (var index = 0; index < rowHasMore.Length && index < _rowMoreButtons.Count; index++)
            {
                rowHasMore[index] = _rowMoreButtons[index] != null;
            }

            return rowHasMore;
        }

        private static bool IsFocusWithin(object focusedElement, DependencyObject target)
        {
            var current = focusedElement as DependencyObject;
            while (current != null)
            {
                if (current == target)
                {
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private async Task LoadHomeAsync(HomeLoadDecision decision)
        {
            if (_isLoadingHome)
            {
                return;
            }

            _isLoadingHome = true;
            var loadGeneration = ++_loadGeneration;

            RefreshButton.IsEnabled = false;
            StatusBlock.Text = decision.StatusText;
            if (decision.ShouldClearExistingContent)
            {
                ClearHomeContent();
            }

            try
            {
#if DEBUG
                if (_developmentFixtureRequest != null)
                {
                    RenderDevelopmentFixtureHome();
                    return;
                }
#endif
                var session = await _sessionStore.LoadAsync();
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                _session = session;
                if (session == null)
                {
                    ClearHomeContent();
                    StatusBlock.Text = "Sign in first.";
                    return;
                }

                using (var httpClient = new HttpClient())
                {
                    var client = EmbyClientFactory.Create(httpClient, session);
                    var continueItemsTask = TryLoadListAsync(() => client.GetResumeItemsAsync(session, 24));
                    var nextUpItemsTask = TryLoadListAsync(() => client.GetNextUpItemsAsync(session, 24));
                    var latestItemsTask = TryLoadListAsync(() => client.GetLatestItemsAsync(session));
                    var libraryViewsTask = TryLoadListAsync(() => client.GetUserViewsAsync(session));
                    await Task.WhenAll(continueItemsTask, nextUpItemsTask, latestItemsTask, libraryViewsTask);

                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    var continueItems = continueItemsTask.Result;
                    var nextUpItems = nextUpItemsTask.Result;
                    var latestItems = latestItemsTask.Result;
                    var libraryViews = libraryViewsTask.Result;
                    _client = client;
                    RenderHome(
                        continueItems,
                        nextUpItems,
                        latestItems,
                        libraryViews,
                        new Dictionary<string, IReadOnlyList<EmbyMediaItem>>(StringComparer.Ordinal),
                        Array.Empty<HomeSectionRow>(),
                        Array.Empty<LibraryContentRow>(),
                        isSupplementalRender: false);

                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    StatusBlock.Text = "Loading more rows...";
                    var libraryPreviewsTask = LoadLibraryPreviewsAsync(client, session, libraryViews, loadGeneration);
                    var configuredRowsTask = LoadConfiguredHomeRowsAsync(client, session, loadGeneration);
                    var popularRowsTask = LoadPopularRowsAsync(client, session, libraryViews, loadGeneration);
                    await Task.WhenAll(libraryPreviewsTask, configuredRowsTask, popularRowsTask);

                    if (!CanApplyLoad(loadGeneration))
                    {
                        return;
                    }

                    var libraryPreviews = libraryPreviewsTask.Result;
                    var configuredRows = configuredRowsTask.Result;
                    var popularRows = popularRowsTask.Result;

                    RenderHome(
                        continueItems,
                        nextUpItems,
                        latestItems,
                        libraryViews,
                        libraryPreviews,
                        configuredRows,
                        popularRows,
                        isSupplementalRender: true);
                }
            }
            catch
            {
                if (!CanApplyLoad(loadGeneration))
                {
                    return;
                }

                var failure = HomeLoadPolicy.ForLoadFailure(_hasRenderedHomeContent);
                if (failure.ShouldClearExistingContent)
                {
                    ClearHomeContent();
                }

                StatusBlock.Text = failure.StatusText;
            }
            finally
            {
                _isLoadingHome = false;
                if (CanApplyLoad(loadGeneration))
                {
                    RefreshButton.IsEnabled = true;
                    if (!HeroPlayButton.IsEnabled &&
                        RowsPanel.Children.Count == 0 &&
                        !string.IsNullOrWhiteSpace(StatusBlock.Text))
                    {
                        RefreshButton.Focus(FocusState.Programmatic);
                    }
                }
            }
        }

#if DEBUG
        private void RenderDevelopmentFixtureHome()
        {
            var fixture = DevelopmentHomeFixture.Create();
            _client = null;
            _session = null;
            _developmentArtworkUris = fixture.ArtworkUris;

            var configuredRows = fixture.ConfiguredRows
                .Select(row => new HomeSectionRow(
                    row.Title,
                    row.SectionId,
                    row.CollectionType,
                    row.ParentItem,
                    row.Items))
                .ToList();

            var popularRows = fixture.PopularRows
                .Select(row => new LibraryContentRow(
                    row.Title,
                    row.Items,
                    CreateLibraryRequest(
                        row.Title,
                        row.CollectionType,
                        row.ParentId,
                        row.SectionId)))
                .ToList();

            RenderHome(
                fixture.ContinueItems,
                fixture.NextUpItems,
                fixture.LatestItems,
                fixture.LibraryViews,
                fixture.LibraryPreviews,
                configuredRows,
                popularRows,
                isSupplementalRender: false);
        }
#endif

        private void RenderHome(
            IReadOnlyList<EmbyMediaItem> continueItems,
            IReadOnlyList<EmbyMediaItem> nextUpItems,
            IReadOnlyList<EmbyMediaItem> latestItems,
            IReadOnlyList<EmbyLibraryView> libraryViews,
            IReadOnlyDictionary<string, IReadOnlyList<EmbyMediaItem>> libraryPreviews,
            IReadOnlyList<HomeSectionRow> configuredRows,
            IReadOnlyList<LibraryContentRow> popularRows,
            bool isSupplementalRender)
        {
            var focusBehavior = HomeLoadPolicy.ForRenderCompleted(_hasRenderedHomeContent, isSupplementalRender);
            var previousFocusTarget = focusBehavior == HomeRenderFocusBehavior.RestoreExistingFocus
                ? CreateHomeFocusTarget(ResolveHomeFocusTarget(FocusManager.GetFocusedElement()) ?? _lastHomeFocusTarget)
                : null;

            ClearRows();
            RenderLibraries(libraryViews, libraryPreviews, configuredRows);
            _hasRenderedHomeContent = true;

            EmbyMediaItem? heroItem = null;
            if (continueItems != null && continueItems.Count > 0)
            {
                heroItem = continueItems[0];
            }
            else if (nextUpItems != null && nextUpItems.Count > 0)
            {
                heroItem = nextUpItems[0];
            }
            else
            {
                heroItem = FindFirstPlayableItem(latestItems);
                if (heroItem == null && latestItems != null && latestItems.Count > 0)
                {
                    heroItem = latestItems[0];
                }
            }

            if (heroItem == null)
            {
                RenderBrowseOnlyHero();
                StatusBlock.Text = _libraryButtons.Count > 0
                    ? "Choose a library below, or refresh Home."
                    : "No playable items found.";
                CompleteHomeRenderFocus(focusBehavior, previousFocusTarget);
                return;
            }

            RenderHero(heroItem);
            StatusBlock.Text = "";

            var renderedRowTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddUniqueRow(renderedRowTitles, "Continue watching", continueItems, null);
            AddUniqueRow(renderedRowTitles, "Next up", nextUpItems, null);

            foreach (var configuredRow in configuredRows)
            {
                AddUniqueRow(
                    renderedRowTitles,
                    configuredRow.Title,
                    configuredRow.Items,
                    CreateLibraryRequest(
                        configuredRow.Title,
                        configuredRow.CollectionType,
                        "",
                        configuredRow.SectionId));
            }

            foreach (var popularRow in popularRows)
            {
                AddUniqueRow(renderedRowTitles, popularRow.Title, popularRow.Items, popularRow.MoreRequest);
            }

            foreach (var view in libraryViews)
            {
                IReadOnlyList<EmbyMediaItem> previewItems;
                if (string.IsNullOrWhiteSpace(view.Id) ||
                    !libraryPreviews.TryGetValue(view.Id, out previewItems) ||
                    previewItems.Count == 0)
                {
                    continue;
                }

                AddUniqueRow(renderedRowTitles, "Latest in " + view.Name, previewItems, CreateLibraryRequest(view));
            }

            AddUniqueRow(renderedRowTitles, "Latest", latestItems, null);
            CompleteHomeRenderFocus(focusBehavior, previousFocusTarget);
        }

        private void CompleteHomeRenderFocus(
            HomeRenderFocusBehavior focusBehavior,
            HomeFocusTarget? previousFocusTarget)
        {
            if (focusBehavior == HomeRenderFocusBehavior.RestoreExistingFocus &&
                TryMoveFocus(ResolveHomeFocusControl(previousFocusTarget)))
            {
                return;
            }

            FocusDailyStart(FocusState.Programmatic);
        }

        private void RenderHero(EmbyMediaItem item)
        {
            _heroItem = item;
            ResetHeroLogo();
            HeroTitleBlock.Text = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            HeroMetaBlock.Text = CreateMeta(item);
            HeroPlayButton.IsEnabled = CanPlay(item);
            HeroDetailsButton.IsEnabled = true;
            HeroPosterImage.Source = null;
            HeroBackdropImage.Source = null;
            HeroPosterFallbackBlock.Visibility = Visibility.Visible;

            var posterArtwork = EmbyArtworkPolicy.SelectPosterArtwork(item, 520);
            var posterImage = CreateBitmapImage(posterArtwork);
            if (posterImage != null)
            {
                HeroPosterImage.Source = posterImage;
                HeroPosterFallbackBlock.Visibility = Visibility.Collapsed;
            }

            var logoArtwork = EmbyArtworkPolicy.SelectLogoArtwork(item, 720);
            var logoImage = CreateBitmapImage(logoArtwork);
            if (logoImage != null)
            {
                HeroLogoImage.Source = logoImage;
                HeroLogoImage.Visibility = Visibility.Visible;
            }

            var heroArtwork = EmbyArtworkPolicy.SelectHeroArtwork(item, 1280);
            var heroImage = CreateBitmapImage(heroArtwork);
            if (heroImage != null)
            {
                HeroBackdropImage.Source = heroImage;
            }
        }

        private void ClearHero()
        {
            _heroItem = null;
            ResetHeroLogo();
            HeroTitleBlock.Text = "Nothing queued yet";
            HeroMetaBlock.Text = "Refresh after signing in to load your Emby home screen.";
            HeroPosterImage.Source = null;
            HeroBackdropImage.Source = null;
            HeroPosterFallbackBlock.Visibility = Visibility.Visible;
            HeroPlayButton.IsEnabled = false;
            HeroDetailsButton.IsEnabled = false;
        }

        private void RenderBrowseOnlyHero()
        {
            _heroItem = null;
            ResetHeroLogo();
            HeroTitleBlock.Text = _libraryButtons.Count > 0 ? "Browse your libraries" : "Nothing queued yet";
            HeroMetaBlock.Text = _libraryButtons.Count > 0
                ? "Pick a library below to start browsing while Home rows refresh."
                : "Refresh after signing in to load your Emby home screen.";
            HeroPosterImage.Source = null;
            HeroBackdropImage.Source = null;
            HeroPosterFallbackBlock.Visibility = Visibility.Visible;
            HeroPlayButton.IsEnabled = false;
            HeroDetailsButton.IsEnabled = false;
        }

        private void ResetHeroLogo()
        {
            HeroLogoImage.Source = null;
            HeroLogoImage.Visibility = Visibility.Collapsed;
            HeroTitleBlock.Visibility = Visibility.Visible;
        }

        private void HeroLogoImage_OnImageOpened(object sender, RoutedEventArgs e)
        {
            if (HeroLogoImage.Source != null)
            {
                HeroTitleBlock.Visibility = Visibility.Collapsed;
            }
        }

        private void HeroLogoImage_OnImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            ResetHeroLogo();
        }

        private void ClearHomeContent()
        {
            ClearHero();
            LibrariesPanel.Children.Clear();
            _libraryButtons.Clear();
            ClearRows();
            _hasRenderedHomeContent = false;
        }

        private void RenderLibraries(
            IReadOnlyList<EmbyLibraryView> libraryViews,
            IReadOnlyDictionary<string, IReadOnlyList<EmbyMediaItem>> libraryPreviews,
            IReadOnlyList<HomeSectionRow> configuredRows)
        {
            LibrariesPanel.Children.Clear();
            _libraryButtons.Clear();

            var viewButtonCount = 0;
            foreach (var view in libraryViews)
            {
                if (string.IsNullOrWhiteSpace(view.Id))
                {
                    continue;
                }

                IReadOnlyList<EmbyMediaItem> previewItems;
                libraryPreviews.TryGetValue(view.Id, out previewItems);
                var button = CreateLibraryButton(view, previewItems ?? Array.Empty<EmbyMediaItem>());
                LibrariesPanel.Children.Add(button);
                _libraryButtons.Add(button);
                viewButtonCount++;
            }

            if (viewButtonCount == 0)
            {
                var movies = new EmbyLibraryView { Name = "Movies", CollectionType = "movies" };
                var tv = new EmbyLibraryView { Name = "TV", CollectionType = "tvshows" };
                var moviesButton = CreateLibraryButton(movies, Array.Empty<EmbyMediaItem>());
                var tvButton = CreateLibraryButton(tv, Array.Empty<EmbyMediaItem>());
                LibrariesPanel.Children.Add(moviesButton);
                LibrariesPanel.Children.Add(tvButton);
                _libraryButtons.Add(moviesButton);
                _libraryButtons.Add(tvButton);
            }

            foreach (var sectionRow in configuredRows)
            {
                if (string.IsNullOrWhiteSpace(sectionRow.SectionId))
                {
                    continue;
                }

                var button = CreateHomeSectionButton(sectionRow);
                LibrariesPanel.Children.Add(button);
                _libraryButtons.Add(button);
            }
        }

        private Button CreateLibraryButton(EmbyLibraryView view, IReadOnlyList<EmbyMediaItem> previewItems)
        {
            var request = CreateLibraryRequest(view);
            var button = new Button
            {
                Width = 250,
                Height = 132,
                Padding = new Thickness(0),
                Tag = request,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true
            };
            button.GotFocus += HomeFocusTarget_OnGotFocus;
            button.Click += LibraryButton_OnClick;
            AutomationProperties.SetName(button, string.IsNullOrWhiteSpace(view.Name) ? "Library" : view.Name);

            var root = new Grid();
            var background = new Border
            {
                Background = (Brush)Application.Current.Resources["AppChromeBrush"],
                BorderBrush = (Brush)Application.Current.Resources["AppHairlineBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
            root.Children.Add(background);

            var previewBrush = CreateLibraryArtworkBrush(view, 640);
            if (previewBrush == null)
            {
                var previewItem = previewItems.FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(item.BackdropImageTag) ||
                    !string.IsNullOrWhiteSpace(item.PrimaryImageTag));
                previewBrush = CreateArtworkBrush(previewItem, 520, preferBackdrop: true);
            }

            if (previewBrush != null)
            {
                root.Children.Add(new Border
                {
                    Background = previewBrush,
                    CornerRadius = new CornerRadius(8),
                    Opacity = GetDoubleResource("TvHomeLibraryArtworkOpacity", 0.88)
                });
                root.Children.Add(new Border
                {
                    Background = (Brush)Application.Current.Resources["AppLibraryArtworkWashBrush"],
                    CornerRadius = new CornerRadius(8)
                });
            }

            root.Children.Add(new Border
            {
                Height = 3,
                VerticalAlignment = VerticalAlignment.Top,
                Background = view.IsTvLibrary
                    ? (Brush)Application.Current.Resources["AppWarmBrush"]
                    : (Brush)Application.Current.Resources["AppAccentBrush"],
                CornerRadius = new CornerRadius(8, 8, 0, 0)
            });

            root.Children.Add(CreateHomeWideCardTextScrim());
            root.Children.Add(new StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Bottom,
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(view.Name) ? "Library" : view.Name,
                        FontSize = 23,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.Resources["AppTextBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    },
                    new TextBlock
                    {
                        Text = CreateLibrarySubtitle(view, previewItems),
                        FontSize = 15,
                        Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    }
                }
            });

            button.Content = root;
            return button;
        }

        private Button CreateHomeSectionButton(HomeSectionRow row)
        {
            var request = CreateLibraryRequest(row.Title, row.CollectionType, "", row.SectionId);
            var button = new Button
            {
                Width = 284,
                Height = 132,
                Padding = new Thickness(0),
                Tag = request,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true
            };
            button.GotFocus += HomeFocusTarget_OnGotFocus;
            button.Click += LibraryButton_OnClick;
            AutomationProperties.SetName(button, row.Title);

            var root = new Grid();
            root.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["AppChromeBrush"],
                BorderBrush = (Brush)Application.Current.Resources["AppHairlineBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            });

            var artworkBrush = CreateHomeSectionArtworkBrush(row, 720);
            if (artworkBrush == null)
            {
                var fallbackItem = row.Items.FirstOrDefault(item =>
                    !string.IsNullOrWhiteSpace(item.BackdropImageTag) ||
                    !string.IsNullOrWhiteSpace(item.ThumbImageTag) ||
                    !string.IsNullOrWhiteSpace(item.PrimaryImageTag));
                artworkBrush = CreateArtworkBrush(fallbackItem, 640, preferBackdrop: true);
            }

            if (artworkBrush != null)
            {
                root.Children.Add(new Border
                {
                    Background = artworkBrush,
                    CornerRadius = new CornerRadius(8),
                    Opacity = GetDoubleResource("TvHomeSectionArtworkOpacity", 0.9)
                });
                root.Children.Add(new Border
                {
                    Background = (Brush)Application.Current.Resources["AppSectionArtworkWashBrush"],
                    CornerRadius = new CornerRadius(8)
                });
            }

            root.Children.Add(new Border
            {
                Width = 4,
                Margin = new Thickness(0, 18, 0, 18),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = (Brush)Application.Current.Resources["AppWarmBrush"],
                CornerRadius = new CornerRadius(0, 3, 3, 0)
            });

            root.Children.Add(CreateHomeWideCardTextScrim());
            root.Children.Add(new StackPanel
            {
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Bottom,
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = row.Title,
                        FontSize = 22,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.Resources["AppTextBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    },
                    new TextBlock
                    {
                        Text = CreateHomeSectionSubtitle(row),
                        FontSize = 15,
                        Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    }
                }
            });

            button.Content = root;
            return button;
        }

        private static Border CreateHomeWideCardTextScrim()
        {
            var scrim = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1)
            };
            scrim.GradientStops.Add(new GradientStop
            {
                Color = (Windows.UI.Color)Application.Current.Resources["AppTransparentColor"],
                Offset = 0
            });
            scrim.GradientStops.Add(new GradientStop
            {
                Color = (Windows.UI.Color)Application.Current.Resources["AppCardScrimColor"],
                Offset = 0.72
            });
            scrim.GradientStops.Add(new GradientStop
            {
                Color = (Windows.UI.Color)Application.Current.Resources["AppCardScrimColor"],
                Offset = 1
            });

            return new Border
            {
                Height = GetDoubleResource("TvHomeWideCardTextScrimHeight", 92),
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = scrim,
                CornerRadius = new CornerRadius(0, 0, 8, 8)
            };
        }

        private static double GetDoubleResource(string key, double fallback)
        {
            var resources = Application.Current.Resources;
            if (resources.ContainsKey(key) && resources[key] is double value)
            {
                return value;
            }

            return fallback;
        }

        private ImageBrush? CreateLibraryArtworkBrush(EmbyLibraryView view, int maxWidth)
        {
            if (view == null || string.IsNullOrWhiteSpace(view.Id))
            {
                return null;
            }

            return CreateArtworkBrush(EmbyArtworkPolicy.SelectLibraryWideArtwork(view, maxWidth));
        }

        private ImageBrush? CreateHomeSectionArtworkBrush(HomeSectionRow row, int maxWidth)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.SectionId))
            {
                return null;
            }

            return CreateArtworkBrush(EmbyArtworkPolicy.SelectHomeSectionWideArtwork(new EmbyHomeSection
            {
                Id = row.SectionId,
                Name = row.Title,
                CollectionType = row.CollectionType,
                ParentItem = row.ParentItem
            }, maxWidth));
        }

        private void AddRow(
            string title,
            IReadOnlyList<EmbyMediaItem>? items,
            LibraryNavigationRequest? moreRequest)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            var rowIndex = _rowFirstButtons.Count;
            var section = new StackPanel
            {
                Spacing = 12
            };

            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 24,
                FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["AppTextBrush"]
            });
            Button? moreButton = null;
            if (moreRequest != null)
            {
                moreButton = new Button
                {
                    Content = "More",
                    MinWidth = 86,
                    MinHeight = 44,
                    Height = 44,
                    Padding = new Thickness(16, 6, 16, 6),
                    FontSize = 16,
                    Tag = moreRequest
                };
                moreButton.Click += LibraryButton_OnClick;
                moreButton.GotFocus += HomeFocusTarget_OnGotFocus;
                AutomationProperties.SetName(moreButton, "More " + title);
                _rowMoreButtonIndexes[moreButton] = rowIndex;
                Grid.SetColumn(moreButton, 1);
                header.Children.Add(moreButton);
            }

            section.Children.Add(header);

            var scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollMode = ScrollMode.Enabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollMode = ScrollMode.Disabled
            };

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 14
            };

            Button? firstRowButton = null;
            foreach (var item in items)
            {
                var itemButton = CreateItemButton(item);
                RegisterRowButton(itemButton, rowIndex);
                if (firstRowButton == null)
                {
                    firstRowButton = itemButton;
                }

                panel.Children.Add(itemButton);
            }

            if (firstRowButton != null)
            {
                _rowFirstButtons.Add(firstRowButton);
                _rowMoreButtons.Add(moreButton);
            }

            scroller.Content = panel;
            section.Children.Add(scroller);
            RowsPanel.Children.Add(section);
        }

        private void ClearRows()
        {
            RowsPanel.Children.Clear();
            _rowFirstButtons.Clear();
            _rowMoreButtons.Clear();
            _rowButtonIndexes.Clear();
            _rowMoreButtonIndexes.Clear();
        }

        private void RegisterRowButton(Button button, int rowIndex)
        {
            button.GotFocus += HomeFocusTarget_OnGotFocus;
            _rowButtonIndexes[button] = rowIndex;
        }

        private void AddUniqueRow(
            HashSet<string> renderedRowTitles,
            string title,
            IReadOnlyList<EmbyMediaItem>? items,
            LibraryNavigationRequest? moreRequest)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            var key = string.IsNullOrWhiteSpace(title) ? "Home section" : title.Trim();
            if (!renderedRowTitles.Add(key))
            {
                return;
            }

            AddRow(key, items, moreRequest);
        }

        private Button CreateItemButton(EmbyMediaItem item)
        {
            var button = new Button
            {
                Width = 172,
                Height = 252,
                Padding = new Thickness(0),
                Tag = item,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                UseSystemFocusVisuals = true
            };
            button.Click += ItemButton_OnClick;
            AutomationProperties.SetName(button, string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name);

            var root = new Grid
            {
                Background = (Brush)Application.Current.Resources["AppRaisedSurfaceBrush"]
            };

            var posterArtwork = EmbyArtworkPolicy.SelectPosterArtwork(item, 420);
            var posterImage = CreateBitmapImage(posterArtwork);
            if (posterImage != null)
            {
                root.Background = new ImageBrush
                {
                    ImageSource = posterImage,
                    Stretch = Stretch.UniformToFill
                };
            }

            root.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["AppHairlineBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6)
            });
            root.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["AppArtworkDimBrush"]
            });

            var overlay = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = (Brush)Application.Current.Resources["AppCardScrimBrush"],
                Padding = new Thickness(12, 10, 12, 10)
            };

            overlay.Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
                        FontSize = 16,
                        FontWeight = Windows.UI.Text.FontWeights.SemiBold,
                        Foreground = (Brush)Application.Current.Resources["AppTextBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 2
                    },
                    new TextBlock
                    {
                        Text = CreateMeta(item),
                        FontSize = 13,
                        Foreground = (Brush)Application.Current.Resources["AppMutedTextBrush"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxLines = 1
                    }
                }
            };

            root.Children.Add(overlay);
            button.Content = root;
            return button;
        }

        private async Task<IReadOnlyList<T>> TryLoadListAsync<T>(Func<Task<IReadOnlyList<T>>> load)
        {
            try
            {
                return await InteractiveRequestGuard.TryGetListOrEmptyAsync(
                    load(),
                    EmbyRequestTimeoutPolicy.InteractiveRequestTimeout);
            }
            catch
            {
                return Array.Empty<T>();
            }
        }

        private async Task<IReadOnlyDictionary<string, IReadOnlyList<EmbyMediaItem>>> LoadLibraryPreviewsAsync(
            EmbyApiClient client,
            EmbySession session,
            IReadOnlyList<EmbyLibraryView> libraryViews,
            int loadGeneration)
        {
            var result = new Dictionary<string, IReadOnlyList<EmbyMediaItem>>(StringComparer.Ordinal);
            var previewTasks = libraryViews
                .Take(12)
                .Where(view => !string.IsNullOrWhiteSpace(view.Id))
                .Select(async view => new
                {
                    ViewId = view.Id,
                    Items = await TryLoadListAsync(() => client.GetLatestItemsAsync(
                        session,
                        view.Id,
                        GuessIncludeItemTypes(view.CollectionType),
                        12))
                })
                .ToList();

            var previews = await Task.WhenAll(previewTasks);
            if (!CanApplyLoad(loadGeneration))
            {
                return result;
            }

            foreach (var preview in previews)
            {
                result[preview.ViewId] = preview.Items;
            }

            return result;
        }

        private async Task<IReadOnlyList<HomeSectionRow>> LoadConfiguredHomeRowsAsync(
            EmbyApiClient client,
            EmbySession session,
            int loadGeneration)
        {
            var sections = await TryLoadListAsync(() => client.GetHomeSectionsAsync(session));
            var rows = new List<HomeSectionRow>();

            var rowTasks = sections
                .Take(16)
                .Where(section =>
                    !string.IsNullOrWhiteSpace(section.Id) &&
                    !IsDuplicateSystemSection(section))
                .Select(async section =>
                {
                    var items = await TryLoadListAsync(() => client.GetHomeSectionItemsAsync(session, section.Id, 24));
                    return items.Count == 0
                        ? null
                        : new HomeSectionRow(
                            CreateSectionTitle(section),
                            section.Id,
                            section.CollectionType,
                            section.ParentItem,
                            items);
                })
                .ToList();

            var loadedRows = await Task.WhenAll(rowTasks);
            foreach (var row in loadedRows)
            {
                if (row != null)
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private async Task<IReadOnlyList<LibraryContentRow>> LoadPopularRowsAsync(
            EmbyApiClient client,
            EmbySession session,
            IReadOnlyList<EmbyLibraryView> libraryViews,
            int loadGeneration)
        {
            var rows = new List<LibraryContentRow>();
            var rowTasks = libraryViews
                .Take(8)
                .Where(view => !string.IsNullOrWhiteSpace(view.Id))
                .Select(async view =>
                {
                    var moreRequest = CreateLibraryRequest(view);
                    var items = await TryLoadListAsync(() => client.GetItemsAsync(session, new EmbyItemsQuery
                    {
                        ParentId = moreRequest.ParentId,
                        IncludeItemTypes = moreRequest.IncludeItemTypes,
                        CollectionTypes = moreRequest.Query.CollectionTypes,
                        MediaTypes = moreRequest.Query.MediaTypes,
                        IsFolder = moreRequest.Query.IsFolder,
                        SortBy = "PlayCount",
                        SortOrder = "Descending",
                        Filters = CombineFilters(moreRequest.Query.Filters, "IsNotFolder"),
                        Limit = 24,
                        Recursive = true
                    }));

                    return items.Count == 0
                        ? null
                        : new LibraryContentRow(
                            CreatePopularTitle(view),
                            items,
                            moreRequest);
                })
                .ToList();

            var loadedRows = await Task.WhenAll(rowTasks);
            foreach (var row in loadedRows)
            {
                if (row != null)
                {
                    rows.Add(row);
                }
            }

            return rows;
        }

        private ImageBrush? CreateArtworkBrush(EmbyMediaItem? item, int maxWidth, bool preferBackdrop)
        {
            if (_client == null || _session == null || item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return null;
            }

            var candidate = preferBackdrop
                ? EmbyArtworkPolicy.SelectHeroArtwork(item, maxWidth)
                : EmbyArtworkPolicy.SelectPosterArtwork(item, maxWidth);

            return CreateArtworkBrush(candidate);
        }

        private ImageBrush? CreateArtworkBrush(EmbyImageCandidate? candidate)
        {
            var image = CreateBitmapImage(candidate);
            if (image == null)
            {
                return null;
            }

            return new ImageBrush
            {
                ImageSource = image,
                Stretch = Stretch.UniformToFill
            };
        }

        private BitmapImage? CreateBitmapImage(EmbyImageCandidate? candidate)
        {
            var uri = CreateArtworkUri(candidate);
            return string.IsNullOrWhiteSpace(uri) ? null : new BitmapImage(new Uri(uri));
        }

        private string? CreateArtworkUri(EmbyImageCandidate? candidate)
        {
            if (candidate == null)
            {
                return null;
            }

#if DEBUG
            string developmentUri;
            if (_developmentArtworkUris.TryGetValue(
                DevelopmentHomeFixture.ArtworkKey(candidate.ItemId, candidate.ImageType),
                out developmentUri))
            {
                return developmentUri;
            }
#endif

            if (_client == null || _session == null)
            {
                return null;
            }

            return _client.GetImageUrl(
                _session,
                candidate.ItemId,
                candidate.ImageType,
                candidate.MaxWidth);
        }

        private static bool IsDuplicateSystemSection(EmbyHomeSection section)
        {
            var title = ((section.Name ?? "") + " " + (section.Subtitle ?? "") + " " + (section.SectionType ?? "")).ToLowerInvariant();
            return title.Contains("continue") ||
                title.Contains("resume") ||
                title.Contains("next up") ||
                title.Contains("nextup") ||
                title.Contains("继续") ||
                title.Contains("接着");
        }

        private static string CreateSectionTitle(EmbyHomeSection section)
        {
            if (!string.IsNullOrWhiteSpace(section.Name))
            {
                return section.Name;
            }

            if (!string.IsNullOrWhiteSpace(section.Subtitle))
            {
                return section.Subtitle;
            }

            return "Home section";
        }

        private static string CreateLibrarySubtitle(EmbyLibraryView view, IReadOnlyList<EmbyMediaItem> previewItems)
        {
            if (previewItems.Count > 0)
            {
                return previewItems.Count + " recent items";
            }

            if (view.IsMovieLibrary)
            {
                return "Film library";
            }

            if (view.IsTvLibrary)
            {
                return "Series library";
            }

            return string.IsNullOrWhiteSpace(view.CollectionType) ? "Media library" : view.CollectionType;
        }

        private static string CreateHomeSectionSubtitle(HomeSectionRow row)
        {
            if (row.Items.Count > 0)
            {
                return row.Items.Count + " items";
            }

            return string.IsNullOrWhiteSpace(row.CollectionType) ? "Server section" : row.CollectionType;
        }

        private static string CreatePopularTitle(EmbyLibraryView view)
        {
            if (view.IsMovieLibrary)
            {
                return "Hot Movies";
            }

            if (view.IsTvLibrary)
            {
                return "Hot TV Series";
            }

            return "Popular in " + (string.IsNullOrWhiteSpace(view.Name) ? "Library" : view.Name);
        }

        private static LibraryNavigationRequest CreateLibraryRequest(EmbyLibraryView view)
        {
            return CreateLibraryRequest(
                string.IsNullOrWhiteSpace(view.Name) ? "Library" : view.Name,
                view.CollectionType,
                view.Id,
                "");
        }

        private static LibraryNavigationRequest CreateLibraryRequest(
            string title,
            string collectionType,
            string parentId,
            string sectionId)
        {
            return new LibraryNavigationRequest(
                title,
                collectionType,
                GuessIncludeItemTypes(collectionType),
                parentId,
                sectionId,
                GuessNavigationQuery(collectionType));
        }

        private static string GuessIncludeItemTypes(string collectionType)
        {
            if (string.Equals(collectionType, "movies", StringComparison.OrdinalIgnoreCase))
            {
                return "Movie";
            }

            if (string.Equals(collectionType, "tvshows", StringComparison.OrdinalIgnoreCase))
            {
                return "Series";
            }

            if (string.Equals(collectionType, "boxsets", StringComparison.OrdinalIgnoreCase))
            {
                return "BoxSet";
            }

            if (string.Equals(collectionType, "playlists", StringComparison.OrdinalIgnoreCase))
            {
                return "Playlist";
            }

            if (string.Equals(collectionType, "music", StringComparison.OrdinalIgnoreCase))
            {
                return "MusicAlbum,Audio";
            }

            if (string.Equals(collectionType, "photos", StringComparison.OrdinalIgnoreCase))
            {
                return "Photo";
            }

            if (string.Equals(collectionType, "homevideos", StringComparison.OrdinalIgnoreCase))
            {
                return "Video";
            }

            return "Movie,Series,Episode,Video,MusicVideo";
        }

        private static LibraryNavigationQuery GuessNavigationQuery(string collectionType)
        {
            if (string.Equals(collectionType, "boxsets", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(collectionType, "playlists", StringComparison.OrdinalIgnoreCase))
            {
                return new LibraryNavigationQuery(isFolder: false, requireItemTypeMatch: true);
            }

            if (string.Equals(collectionType, "photos", StringComparison.OrdinalIgnoreCase))
            {
                return new LibraryNavigationQuery(mediaTypes: "Photo", requireItemTypeMatch: true);
            }

            return LibraryNavigationQuery.Empty;
        }

        private static string CombineFilters(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second ?? "";
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first ?? "";
            }

            return first + "," + second;
        }

        private bool FocusDailyStart(FocusState focusState)
        {
            if (HeroPlayButton.IsEnabled && HeroPlayButton.Focus(focusState))
            {
                _lastHomeFocusTarget = HeroPlayButton;
                return true;
            }

            var firstLibraryButton = GetFirstLibraryButton();
            if (firstLibraryButton != null && firstLibraryButton.Focus(focusState))
            {
                _lastHomeFocusTarget = firstLibraryButton;
                return true;
            }

            if (!RefreshButton.Focus(focusState))
            {
                return false;
            }

            _lastHomeFocusTarget = RefreshButton;
            return true;
        }

        private void ItemButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button == null ? null : button.Tag as EmbyMediaItem;
            NavigateToDetails(item);
        }

        private void LibraryButton_OnClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var request = button == null ? null : button.Tag as LibraryNavigationRequest;
            if (request == null)
            {
                return;
            }

            Frame.Navigate(typeof(LibraryPage), request);
        }

        private void NavigateToDetails(EmbyMediaItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Id))
            {
                return;
            }

            var itemName = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            Frame.Navigate(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(item.Id, itemName));
        }

        private bool CanApplyLoad(int loadGeneration)
        {
            return !_isUnloaded && loadGeneration == _loadGeneration;
        }

        private static EmbyMediaItem? FindFirstPlayableItem(IReadOnlyList<EmbyMediaItem>? items)
        {
            if (items == null)
            {
                return null;
            }

            foreach (var item in items)
            {
                if (CanPlay(item))
                {
                    return item;
                }
            }

            return null;
        }

        private static bool CanPlay(EmbyMediaItem? item)
        {
            return item != null
                && !string.IsNullOrWhiteSpace(item.Id)
                && (string.Equals(item.Type, "Movie", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Type, "Episode", StringComparison.OrdinalIgnoreCase));
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

            if (item.UserData != null && item.UserData.PlaybackPositionTicks > 0)
            {
                meta += " / Resume";
            }

            return meta;
        }

        private sealed class HomeSectionRow
        {
            public HomeSectionRow(
                string title,
                string sectionId,
                string collectionType,
                EmbyMediaItem parentItem,
                IReadOnlyList<EmbyMediaItem> items)
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Home section" : title;
                SectionId = sectionId ?? "";
                CollectionType = collectionType ?? "";
                ParentItem = parentItem ?? new EmbyMediaItem();
                Items = items ?? Array.Empty<EmbyMediaItem>();
            }

            public string Title { get; }

            public string SectionId { get; }

            public string CollectionType { get; }

            public EmbyMediaItem ParentItem { get; }

            public IReadOnlyList<EmbyMediaItem> Items { get; }
        }

        private sealed class LibraryContentRow
        {
            public LibraryContentRow(
                string title,
                IReadOnlyList<EmbyMediaItem> items,
                LibraryNavigationRequest moreRequest)
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Library" : title;
                Items = items ?? Array.Empty<EmbyMediaItem>();
                MoreRequest = moreRequest;
            }

            public string Title { get; }

            public IReadOnlyList<EmbyMediaItem> Items { get; }

            public LibraryNavigationRequest MoreRequest { get; }
        }
    }
}
