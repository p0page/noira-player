using System;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Storage;
using NextGenEmby.App.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.System;

namespace NextGenEmby.App
{
    public sealed partial class MainPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();

        public MainPage()
        {
            InitializeComponent();
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
                    NavigateHome();
                }
            }
            catch
            {
            }
        }

        private void Page_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.GamepadY)
            {
                NavigateSearch();
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.GamepadB && ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
                e.Handled = true;
            }
        }

        public void NavigateHome()
        {
            NavigateTo(typeof(HomePage));
        }

        private void NavigateLogin()
        {
            NavigateTo(typeof(LoginPage));
        }

        private void NavigateLibrary(LibraryNavigationRequest request)
        {
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
        }

        private void Home_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateHome();
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
    }
}
