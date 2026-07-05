using System;
using NextGenEmby.Core.Input;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Storage;
using NextGenEmby.App.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.System;

namespace NextGenEmby.App
{
    public sealed partial class MainPage : Page
    {
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
                    NavigateHome(replaceHistory: true);
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
                ContentFrame.GoBack();
                e.Handled = true;
            }
        }

        private static bool IsBackKey(VirtualKey key)
        {
            return key == VirtualKey.GamepadB ||
                key == VirtualKey.Escape ||
                key == VirtualKey.GoBack;
        }

        public void NavigateHome()
        {
            NavigateHome(replaceHistory: true);
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
            ApplyShellChrome(e.SourcePageType == typeof(PlaybackPage));
        }

        private void ApplyShellChrome(bool isPlayback)
        {
            ShellHeader.Visibility = isPlayback ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetRow(ContentFrame, isPlayback ? 0 : 1);
            Grid.SetRowSpan(ContentFrame, isPlayback ? 2 : 1);
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

        private static bool IsSameLibraryRequest(
            LibraryNavigationRequest? current,
            LibraryNavigationRequest next)
        {
            return current != null &&
                string.Equals(current.Title, next.Title, StringComparison.Ordinal) &&
                string.Equals(current.CollectionType, next.CollectionType, StringComparison.Ordinal) &&
                string.Equals(current.IncludeItemTypes, next.IncludeItemTypes, StringComparison.Ordinal);
        }

        private bool IsPlaybackPageActive()
        {
            return ContentFrame.CurrentSourcePageType == typeof(PlaybackPage);
        }
    }
}
