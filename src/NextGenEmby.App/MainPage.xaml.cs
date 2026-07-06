using System;
using System.Threading.Tasks;
using NextGenEmby.Core.Diagnostics;
using NextGenEmby.Core.Input;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.App.Views;
using Windows.Storage;
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
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private LibraryNavigationRequest? _currentLibraryRequest;

        public MainPage()
        {
            InitializeComponent();
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
            if (!IsFocusWithin(focusedElement, ShellHeader) &&
                !IsFocusWithin(originalSource, ShellHeader))
            {
                return false;
            }

            var focusTarget = ContentFrame.Content as ITvContentFocusTarget;
            return focusTarget != null && focusTarget.FocusDefaultContent();
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
            ShellHeader.Visibility = isPlayback ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetRow(ContentFrame, isPlayback ? 0 : 1);
            Grid.SetRowSpan(ContentFrame, isPlayback ? 2 : 1);
        }

        private void ApplyShellButtonState(Type pageType, LibraryNavigationRequest? libraryRequest)
        {
            SetShellButtonActive(HomeButton, pageType == typeof(HomePage) || pageType == typeof(LoginPage));
            SetShellButtonActive(MoviesButton, pageType == typeof(LibraryPage) && libraryRequest != null && libraryRequest.IsMovies);
            SetShellButtonActive(TvButton, pageType == typeof(LibraryPage) && libraryRequest != null && libraryRequest.IsTv);
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
            if (pageType == typeof(PlaybackPage))
            {
                return;
            }

            if (pageType == typeof(LibraryPage) && libraryRequest != null)
            {
                if (libraryRequest.IsMovies)
                {
                    MoviesButton.Focus(FocusState.Programmatic);
                    return;
                }

                if (libraryRequest.IsTv)
                {
                    TvButton.Focus(FocusState.Programmatic);
                    return;
                }
            }

            if (pageType == typeof(SearchPage))
            {
                SearchButton.Focus(FocusState.Programmatic);
                return;
            }

            if (pageType == typeof(SettingsPage))
            {
                SettingsButton.Focus(FocusState.Programmatic);
                return;
            }

            HomeButton.Focus(FocusState.Programmatic);
        }

        private void Home_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateHome(replaceHistory: false);
        }

        private void Movies_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateLibrary(new LibraryNavigationRequest("Movies", "movies", "Movie"));
        }

        private void Tv_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateLibrary(new LibraryNavigationRequest("TV Shows", "tvshows", "Series"));
        }

        private void Search_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateSearch();
        }

        private void Settings_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateSettings();
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
