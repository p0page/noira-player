using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Input;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.App.Views;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.System;

namespace NextGenEmby.App
{
    public sealed partial class MainPage : Page
    {
#if DEBUG
        private const string DevelopmentCommandFileName = "dev-command.json";
#endif
        private const double GuideCollapsedWidth = 72d;
        private const double GuideExpandedWidth = 248d;
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private LibraryNavigationRequest? _currentLibraryRequest;
        private Control? _guideReturnFocusTarget;
        private GuideNavigationDestination _guideSelectedDestination = GuideNavigationDestination.Home;
        private bool _guideOpen;
        private bool _focusContentAfterGuideNavigation;

        public MainPage()
        {
            InitializeComponent();
            ApplyGuideOpenState(isOpen: false);
            AddHandler(KeyDownEvent, new KeyEventHandler(Page_OnKeyDown), true);
            ContentFrame.Navigated += ContentFrame_OnNavigated;
            NavigateLogin();
            Loaded += MainPage_OnLoaded;
        }

        private async void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_OnLoaded;
            try
            {
                var session = await _sessionStore.LoadAsync();
                if (session != null)
                {
                    if (ContentFrame.CurrentSourcePageType == typeof(LoginPage))
                    {
                        NavigateHome(replaceHistory: true);
                    }

#if DEBUG
                    await TryRunDevelopmentCommandAsync();
#endif
                }
            }
            catch
            {
            }
        }

        private void Page_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (IsPlaybackPageActive())
            {
                return;
            }

            if (TryApplyGuideNavigationKey(e))
            {
                e.Handled = true;
                return;
            }

            if (IsDownKey(e.Key) && TryFocusContentFromShell(e.OriginalSource))
            {
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.GamepadY)
            {
                NavigateSearch();
                e.Handled = true;
                return;
            }

            if (GlobalBackInputPolicy.ShouldGoBack(
                e.Handled,
                IsPlaybackPageActive(),
                ContentFrame.CanGoBack,
                IsBackKey(e.Key)))
            {
                PlaybackDiagnosticsLog.WriteLine("MainPage.GoBack key=" + e.Key);
                ContentFrame.GoBack();
                e.Handled = true;
            }
        }

        private bool TryFocusContentFromShell(object originalSource)
        {
            var focusedElement = FocusManager.GetFocusedElement();
            if (!IsFocusWithin(focusedElement, GuideRail) &&
                !IsFocusWithin(originalSource, GuideRail))
            {
                return false;
            }

            var focusTarget = ContentFrame.Content as ITvContentFocusTarget;
            return focusTarget != null && focusTarget.FocusDefaultContent();
        }

        private bool TryApplyGuideNavigationKey(KeyRoutedEventArgs e)
        {
            var menuKeyPressed = IsMenuKey(e.Key);
            var backKeyPressed = IsBackKey(e.Key);
            var selectKeyPressed = _guideOpen && IsSelectKey(e.Key);
            var moveUpKeyPressed = _guideOpen && IsUpKey(e.Key);
            var moveDownKeyPressed = _guideOpen && IsDownKey(e.Key);

            if (!menuKeyPressed && !backKeyPressed && !selectKeyPressed && !moveUpKeyPressed && !moveDownKeyPressed)
            {
                return false;
            }

            var selectedDestination = _guideOpen
                ? _guideSelectedDestination
                : ResolveActiveGuideDestination(ContentFrame.CurrentSourcePageType, _currentLibraryRequest);
            var decision = GuideNavigationPolicy.GetDecision(
                e.Handled,
                IsPlaybackPageActive(),
                _guideOpen,
                menuKeyPressed,
                backKeyPressed,
                selectKeyPressed,
                moveUpKeyPressed,
                moveDownKeyPressed,
                selectedDestination);

            switch (decision.Action)
            {
                case GuideNavigationAction.OpenGuide:
                    OpenGuide();
                    return true;

                case GuideNavigationAction.CloseGuide:
                    CloseGuide(decision.ShouldRestorePreviousFocus);
                    return true;

                case GuideNavigationAction.MoveSelection:
                    _guideSelectedDestination = decision.Destination;
                    FocusGuideDestination(decision.Destination, FocusState.Keyboard);
                    return true;

                case GuideNavigationAction.Navigate:
                    NavigateGuideDestination(decision.Destination);
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsBackKey(VirtualKey key)
        {
            return key == VirtualKey.GamepadB ||
                key == VirtualKey.Escape ||
                key == VirtualKey.GoBack;
        }

        private static bool IsMenuKey(VirtualKey key)
        {
            return key == VirtualKey.M ||
                key == VirtualKey.GamepadMenu ||
                key == VirtualKey.Application;
        }

        private static bool IsSelectKey(VirtualKey key)
        {
            return key == VirtualKey.Enter ||
                key == VirtualKey.Space ||
                key == VirtualKey.GamepadA;
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

        public void NavigateHome()
        {
            NavigateHome(replaceHistory: true);
#if DEBUG
            _ = TryRunDevelopmentCommandAsync();
#endif
        }

        private void NavigateHome(bool replaceHistory)
        {
            NavigateTo(typeof(HomePage));
            if (replaceHistory)
            {
                ContentFrame.BackStack.Clear();
            }
        }

        private void NavigateLogin()
        {
            NavigateTo(typeof(LoginPage));
        }

        private void NavigateLibrary(LibraryNavigationRequest request)
        {
            if (ContentFrame.CurrentSourcePageType == typeof(LibraryPage) &&
                IsSameLibraryRequest(_currentLibraryRequest, request))
            {
                return;
            }

            NavigateTo(typeof(LibraryPage), request);
        }

        private void NavigateSearch()
        {
            NavigateTo(typeof(SearchPage));
        }

        private void NavigateSettings()
        {
            NavigateTo(typeof(SettingsPage));
        }

        private void NavigateTo(Type pageType, object? parameter = null)
        {
            if (parameter == null && ContentFrame.CurrentSourcePageType == pageType)
            {
                return;
            }

            PlaybackDiagnosticsLog.WriteLine(
                "MainPage.NavigateTo page=" + pageType.Name +
                " parameter=" + (parameter == null ? "null" : parameter.GetType().Name));
            ContentFrame.Navigate(pageType, parameter);
            _currentLibraryRequest = pageType == typeof(LibraryPage)
                ? parameter as LibraryNavigationRequest
                : null;
        }

        private void ContentFrame_OnNavigated(object sender, NavigationEventArgs e)
        {
            _currentLibraryRequest = e.SourcePageType == typeof(LibraryPage)
                ? e.Parameter as LibraryNavigationRequest
                : null;
            PlaybackDiagnosticsLog.WriteLine(
                "MainPage.Navigated page=" + e.SourcePageType.Name +
                " parameter=" + (e.Parameter == null ? "null" : e.Parameter.GetType().Name) +
                " canGoBack=" + ContentFrame.CanGoBack);
            ApplyShellChrome(e.SourcePageType == typeof(PlaybackPage));
            ApplyShellButtonState(e.SourcePageType, _currentLibraryRequest);
            ApplyNavigationFocus(e.SourcePageType, _currentLibraryRequest, e.NavigationMode);
        }

        private void ApplyShellChrome(bool isPlayback)
        {
            GuideRail.Visibility = isPlayback ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetColumn(ContentFrame, isPlayback ? 0 : 1);
            Grid.SetColumnSpan(ContentFrame, isPlayback ? 2 : 1);
        }

        private void ApplyShellButtonState(Type pageType, LibraryNavigationRequest? libraryRequest)
        {
            SetShellButtonActive(HomeButton, pageType == typeof(HomePage) || pageType == typeof(LoginPage));
            SetShellButtonActive(MoviesButton, pageType == typeof(LibraryPage) && libraryRequest != null && libraryRequest.IsMovies);
            SetShellButtonActive(TvButton, pageType == typeof(LibraryPage) && libraryRequest != null && libraryRequest.IsTv);
            SetShellButtonActive(LiveTvButton, IsLibraryCollection(pageType, libraryRequest, "livetv"));
            SetShellButtonActive(CollectionsButton, IsLibraryCollection(pageType, libraryRequest, "boxsets"));
            SetShellButtonActive(MusicButton, IsLibraryCollection(pageType, libraryRequest, "music"));
            SetShellButtonActive(PhotosButton, IsLibraryCollection(pageType, libraryRequest, "photos"));
            SetShellButtonActive(SearchButton, pageType == typeof(SearchPage));
            SetShellButtonActive(SettingsButton, pageType == typeof(SettingsPage));
        }

        private static void SetShellButtonActive(Button button, bool isActive)
        {
            var resources = Application.Current.Resources;
            button.Background = (Brush)resources[isActive ? "AppRaisedSurfaceBrush" : "AppTransparentBrush"];
            button.BorderBrush = (Brush)resources[isActive ? "AppAccentBrush" : "AppTransparentBrush"];
            button.Foreground = (Brush)resources[isActive ? "AppTextBrush" : "AppMutedTextBrush"];
        }

        private void ApplyNavigationFocus(
            Type pageType,
            LibraryNavigationRequest? libraryRequest,
            NavigationMode navigationMode)
        {
            var contentFocusTarget = ContentFrame.Content as ITvContentFocusTarget;
            if (_focusContentAfterGuideNavigation)
            {
                _focusContentAfterGuideNavigation = false;
                if (contentFocusTarget != null && contentFocusTarget.FocusDefaultContent())
                {
                    return;
                }
            }

            var focusTarget = ShellNavigationFocusPolicy.GetFocusTarget(
                pageType == typeof(PlaybackPage),
                navigationMode == NavigationMode.Back,
                contentFocusTarget != null);

            if (focusTarget == ShellNavigationFocusTarget.Content &&
                contentFocusTarget != null &&
                contentFocusTarget.FocusDefaultContent())
            {
                return;
            }

            if (focusTarget == ShellNavigationFocusTarget.Shell ||
                focusTarget == ShellNavigationFocusTarget.Content)
            {
                FocusShellButton(pageType, libraryRequest);
            }
        }

        private void FocusShellButton(Type pageType, LibraryNavigationRequest? libraryRequest)
        {
            FocusShellButton(pageType, libraryRequest, FocusState.Programmatic);
        }

        private void FocusShellButton(
            Type pageType,
            LibraryNavigationRequest? libraryRequest,
            FocusState focusState)
        {
            if (pageType == typeof(PlaybackPage))
            {
                return;
            }

            if (pageType == typeof(LibraryPage) && libraryRequest != null)
            {
                var libraryButton = GetGuideButtonForLibrary(libraryRequest);
                if (libraryButton != null)
                {
                    libraryButton.Focus(focusState);
                    return;
                }
            }

            if (pageType == typeof(SearchPage))
            {
                SearchButton.Focus(focusState);
                return;
            }

            if (pageType == typeof(SettingsPage))
            {
                SettingsButton.Focus(focusState);
                return;
            }

            HomeButton.Focus(focusState);
        }

        private bool FocusGuideDestination(
            GuideNavigationDestination destination,
            FocusState focusState)
        {
            var button = GetGuideButtonForDestination(destination);
            return button != null && button.Focus(focusState);
        }

        private void Home_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.Home);
        }

        private void Movies_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.Movies);
        }

        private void Tv_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.Tv);
        }

        private void Search_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.Search);
        }

        private void LiveTv_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.LiveTv);
        }

        private void Collections_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.Collections);
        }

        private void Music_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.Music);
        }

        private void Photos_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.Photos);
        }

        private void Settings_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateGuideDestination(GuideNavigationDestination.Settings);
        }

        private void NavigateGuideDestination(GuideNavigationDestination destination)
        {
            _guideSelectedDestination = destination;
            _focusContentAfterGuideNavigation = _guideOpen ||
                IsFocusWithin(FocusManager.GetFocusedElement(), GuideRail);
            CloseGuide(restorePreviousFocus: false);

            switch (destination)
            {
                case GuideNavigationDestination.Home:
                    NavigateHome(replaceHistory: false);
                    return;

                case GuideNavigationDestination.Search:
                    NavigateSearch();
                    return;

                case GuideNavigationDestination.Movies:
                    NavigateLibrary(new LibraryNavigationRequest("Movies", "movies", "Movie"));
                    return;

                case GuideNavigationDestination.Tv:
                    NavigateLibrary(new LibraryNavigationRequest("TV Shows", "tvshows", "Series"));
                    return;

                case GuideNavigationDestination.LiveTv:
                    NavigateLibrary(new LibraryNavigationRequest("Live TV", "livetv", "TvChannel"));
                    return;

                case GuideNavigationDestination.Collections:
                    NavigateLibrary(new LibraryNavigationRequest("Collections", "boxsets", "BoxSet"));
                    return;

                case GuideNavigationDestination.Music:
                    NavigateLibrary(new LibraryNavigationRequest("Music", "music", "MusicAlbum,Audio"));
                    return;

                case GuideNavigationDestination.Photos:
                    NavigateLibrary(new LibraryNavigationRequest("Photos", "photos", "Photo"));
                    return;

                case GuideNavigationDestination.Settings:
                    NavigateSettings();
                    return;
            }
        }

        private void OpenGuide()
        {
            _guideReturnFocusTarget = FocusManager.GetFocusedElement() as Control;
            _guideSelectedDestination = ResolveActiveGuideDestination(
                ContentFrame.CurrentSourcePageType,
                _currentLibraryRequest);
            ApplyGuideOpenState(isOpen: true);
            _ = Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () => FocusGuideDestination(_guideSelectedDestination, FocusState.Keyboard));
        }

        private void CloseGuide(bool restorePreviousFocus)
        {
            ApplyGuideOpenState(isOpen: false);
            if (!restorePreviousFocus)
            {
                return;
            }

            if (_guideReturnFocusTarget != null &&
                _guideReturnFocusTarget.Focus(FocusState.Keyboard))
            {
                return;
            }

            var contentFocusTarget = ContentFrame.Content as ITvContentFocusTarget;
            if (contentFocusTarget != null)
            {
                contentFocusTarget.FocusDefaultContent();
            }
        }

        private void ApplyGuideOpenState(bool isOpen)
        {
            _guideOpen = isOpen;
            GuideColumn.Width = new GridLength(isOpen ? GuideExpandedWidth : GuideCollapsedWidth);
            GuideTitleLabel.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
            SetGuideLabelVisibility(HomeGuideLabel, isOpen);
            SetGuideLabelVisibility(SearchGuideLabel, isOpen);
            SetGuideLabelVisibility(MoviesGuideLabel, isOpen);
            SetGuideLabelVisibility(TvGuideLabel, isOpen);
            SetGuideLabelVisibility(LiveTvGuideLabel, isOpen);
            SetGuideLabelVisibility(CollectionsGuideLabel, isOpen);
            SetGuideLabelVisibility(MusicGuideLabel, isOpen);
            SetGuideLabelVisibility(PhotosGuideLabel, isOpen);
            SetGuideLabelVisibility(SettingsGuideLabel, isOpen);
        }

        private static void SetGuideLabelVisibility(TextBlock label, bool isOpen)
        {
            label.Visibility = isOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private GuideNavigationDestination ResolveActiveGuideDestination(
            Type pageType,
            LibraryNavigationRequest? libraryRequest)
        {
            if (pageType == typeof(SearchPage))
            {
                return GuideNavigationDestination.Search;
            }

            if (pageType == typeof(SettingsPage))
            {
                return GuideNavigationDestination.Settings;
            }

            if (pageType == typeof(LibraryPage) && libraryRequest != null)
            {
                if (libraryRequest.IsMovies)
                {
                    return GuideNavigationDestination.Movies;
                }

                if (libraryRequest.IsTv)
                {
                    return GuideNavigationDestination.Tv;
                }

                switch (libraryRequest.CollectionType)
                {
                    case "livetv":
                        return GuideNavigationDestination.LiveTv;
                    case "boxsets":
                        return GuideNavigationDestination.Collections;
                    case "music":
                        return GuideNavigationDestination.Music;
                    case "photos":
                        return GuideNavigationDestination.Photos;
                }
            }

            return GuideNavigationDestination.Home;
        }

#if DEBUG
        private async Task TryRunDevelopmentCommandAsync()
        {
            try
            {
                await Task.Delay(1000);
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(DevelopmentCommandFileName);
                var file = item as StorageFile;
                if (file == null)
                {
                    await WriteDevelopmentCommandResultAsync("missing", DevelopmentCommandFileName);
                    return;
                }

                var json = await FileIO.ReadTextAsync(file);
                DevelopmentNavigationCommand? command;
                string error;
                if (!DevelopmentNavigationCommand.TryParseJson(json, out command, out error) ||
                    command == null)
                {
                    await WriteDevelopmentCommandResultAsync("parse-failed", error);
                    return;
                }

                await WriteDevelopmentCommandResultAsync("running", command.Route);
                await PlaybackDiagnosticsLog.WriteLineAsync("DevelopmentCommand running route=" + command.Route);
                RunDevelopmentCommand(command);
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                await PlaybackDiagnosticsLog.WriteLineAsync("DevelopmentCommand completed route=" + command.Route);
                await WriteDevelopmentCommandResultAsync("completed", command.Route);
            }
            catch (Exception ex)
            {
                await WriteDevelopmentCommandResultAsync("exception", ex.GetType().FullName ?? ex.Message);
            }
        }

        private static async Task WriteDevelopmentCommandResultAsync(string status, string detail)
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    "dev-command-result.txt",
                    CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, status + Environment.NewLine + (detail ?? ""));
            }
            catch
            {
            }
        }

        private void RunDevelopmentCommand(DevelopmentNavigationCommand command)
        {
            switch (command.Route)
            {
                case "home":
                    NavigateHome(replaceHistory: false);
                    return;

                case "movies":
                    NavigateLibrary(new LibraryNavigationRequest("Movies", "movies", "Movie"));
                    return;

                case "tv":
                    NavigateLibrary(new LibraryNavigationRequest("TV Shows", "tvshows", "Series"));
                    return;

                case "search":
                    NavigateSearch();
                    return;

                case "settings":
                    NavigateSettings();
                    return;

                case "details":
                    NavigateTo(typeof(MediaDetailsPage), new MediaDetailsNavigationRequest(command.ItemId, command.ItemName));
                    return;

                case "playback":
                    NavigateTo(
                        typeof(PlaybackPage),
                        new PlaybackLaunchRequest(
                            command.ItemId,
                            command.ItemName,
                            command.StartPositionTicks,
                            command.MediaSourceId,
                            forceSdrOutput: command.ForceSdrOutput));
                    return;
            }
        }
#endif

        private static bool IsSameLibraryRequest(
            LibraryNavigationRequest? current,
            LibraryNavigationRequest next)
        {
            return current != null &&
                string.Equals(current.Title, next.Title, StringComparison.Ordinal) &&
                string.Equals(current.CollectionType, next.CollectionType, StringComparison.Ordinal) &&
                string.Equals(current.IncludeItemTypes, next.IncludeItemTypes, StringComparison.Ordinal) &&
                string.Equals(current.ParentId, next.ParentId, StringComparison.Ordinal) &&
                string.Equals(current.SectionId, next.SectionId, StringComparison.Ordinal);
        }

        private static bool IsLibraryCollection(
            Type pageType,
            LibraryNavigationRequest? libraryRequest,
            string collectionType)
        {
            return pageType == typeof(LibraryPage) &&
                libraryRequest != null &&
                string.Equals(libraryRequest.CollectionType, collectionType, StringComparison.Ordinal);
        }

        private Button? GetGuideButtonForLibrary(LibraryNavigationRequest libraryRequest)
        {
            if (libraryRequest.IsMovies)
            {
                return MoviesButton;
            }

            if (libraryRequest.IsTv)
            {
                return TvButton;
            }

            switch (libraryRequest.CollectionType)
            {
                case "livetv":
                    return LiveTvButton;
                case "boxsets":
                    return CollectionsButton;
                case "music":
                    return MusicButton;
                case "photos":
                    return PhotosButton;
                default:
                    return null;
            }
        }

        private Button? GetGuideButtonForDestination(GuideNavigationDestination destination)
        {
            switch (destination)
            {
                case GuideNavigationDestination.Home:
                    return HomeButton;
                case GuideNavigationDestination.Search:
                    return SearchButton;
                case GuideNavigationDestination.Movies:
                    return MoviesButton;
                case GuideNavigationDestination.Tv:
                    return TvButton;
                case GuideNavigationDestination.LiveTv:
                    return LiveTvButton;
                case GuideNavigationDestination.Collections:
                    return CollectionsButton;
                case GuideNavigationDestination.Music:
                    return MusicButton;
                case GuideNavigationDestination.Photos:
                    return PhotosButton;
                case GuideNavigationDestination.Settings:
                    return SettingsButton;
                default:
                    return null;
            }
        }

        private bool IsPlaybackPageActive()
        {
            return ContentFrame.CurrentSourcePageType == typeof(PlaybackPage);
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
    }
}
