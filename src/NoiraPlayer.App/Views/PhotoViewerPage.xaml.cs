using System;
using System.Net.Http;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Services;
using NoiraPlayer.App.Storage;
using NoiraPlayer.Core.Input;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace NoiraPlayer.App.Views
{
    public sealed partial class PhotoViewerPage : Page, ITvContentFocusTarget
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private PhotoViewerNavigationRequest? _request;

        public PhotoViewerPage()
        {
            InitializeComponent();
            AddHandler(KeyDownEvent, new KeyEventHandler(PhotoViewerPage_OnKeyDown), true);
            Loaded += PhotoViewerPage_OnLoaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _request = e.Parameter as PhotoViewerNavigationRequest;
            var itemName = _request == null ? "" : _request.ItemName;
            TitleBlock.Text = string.IsNullOrWhiteSpace(itemName) ? "Photo" : itemName;
        }

        private async void PhotoViewerPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= PhotoViewerPage_OnLoaded;
            FocusDefaultContent();

            if (_request == null || string.IsNullOrWhiteSpace(_request.ItemId))
            {
                ShowFallback("Photo unavailable", "The selected photo is missing an item id.");
                return;
            }

            var session = await _sessionStore.LoadAsync();
            if (session == null)
            {
                ShowFallback("Sign in first", "Open Settings or restart the app after signing in.");
                return;
            }

            using (var httpClient = new HttpClient())
            {
                var client = EmbyClientFactory.Create(httpClient, session);
                PhotoImage.Source = new BitmapImage(new Uri(client.GetImageUrl(session, _request.ItemId, "Primary", 1920)));
            }
        }

        public bool FocusDefaultContent()
        {
            return BackButton.Focus(FocusState.Keyboard);
        }

        private void BackButton_OnClick(object sender, RoutedEventArgs e)
        {
            NavigateBack();
        }

        private void PhotoViewerPage_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (!PhotoViewerInputPolicy.ShouldGoBack(Frame.CanGoBack, IsBackKey(e.Key)))
            {
                return;
            }

            e.Handled = true;
            NavigateBack();
        }

        private void NavigateBack()
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private static bool IsBackKey(VirtualKey key)
        {
            return key == VirtualKey.GamepadB ||
                key == VirtualKey.Escape ||
                key == VirtualKey.GoBack;
        }

        private void PhotoImage_OnOpened(object sender, RoutedEventArgs e)
        {
            FallbackPanel.Visibility = Visibility.Collapsed;
            StatusBlock.Text = "Photo";
        }

        private void PhotoImage_OnFailed(object sender, ExceptionRoutedEventArgs e)
        {
            ShowFallback("Photo unavailable", "This photo could not be loaded from the server.");
        }

        private void ShowFallback(string title, string body)
        {
            StatusBlock.Text = "Needs attention";
            FallbackTitleBlock.Text = title;
            FallbackBodyBlock.Text = body;
            FallbackPanel.Visibility = Visibility.Visible;
        }
    }
}
