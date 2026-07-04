using System;
using NextGenEmby.App.Views;
using Windows.UI.Xaml.Controls;
using muxc = Microsoft.UI.Xaml.Controls;

namespace NextGenEmby.App
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            NavigateTo(typeof(LoginPage));
            ShellNav.SelectedItem = LoginNavItem;
        }

        private void ShellNav_OnSelectionChanged(muxc.NavigationView sender, muxc.NavigationViewSelectionChangedEventArgs args)
        {
            var item = args.SelectedItem as muxc.NavigationViewItem;
            if (item == null)
            {
                return;
            }

            var tag = item.Tag as string;
            NavigateTo(tag == "home" ? typeof(HomePage) : typeof(LoginPage));
        }

        private void NavigateTo(Type pageType)
        {
            if (ContentFrame.CurrentSourcePageType != pageType)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }
}
