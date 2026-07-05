using System;
using NextGenEmby.App.Storage;
using NextGenEmby.App.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using muxc = Microsoft.UI.Xaml.Controls;

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

        private void ShellNav_OnSelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            var item = args.SelectedItem as muxc.NavigationViewItem;
            if (item == null)
            {
                return;
            }

            var tag = item.Tag as string;
            if (tag == "home")
            {
                NavigateTo(typeof(HomePage));
            }
            else if (tag == "playback")
            {
                NavigateTo(typeof(PlaybackPage));
            }
            else
            {
                NavigateTo(typeof(LoginPage));
            }
        }

        public void NavigateHome()
        {
            SelectNavigationItem("home");
            NavigateTo(typeof(HomePage));
        }

        private void NavigateLogin()
        {
            SelectNavigationItem("login");
            NavigateTo(typeof(LoginPage));
        }

        private void NavigateTo(Type pageType)
        {
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }

        private void SelectNavigationItem(string tag)
        {
            foreach (var menuItem in ShellNav.MenuItems)
            {
                var item = menuItem as muxc.NavigationViewItem;
                if (item != null && string.Equals(item.Tag as string, tag, StringComparison.Ordinal))
                {
                    ShellNav.SelectedItem = item;
                    return;
                }
            }
        }
    }
}
