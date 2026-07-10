using System;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using NoiraPlayer.App.Web;
using NoiraPlayer.App.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NoiraPlayer.App
{
    public sealed partial class MainPage : Page
    {
        private const string PackagedWebAppHostName = "app.noira.local";
        private const string PackagedWebAppFolderName = "WebCode";
        private const string PackagedWebAppUrl = "https://app.noira.local/index.html";
        private readonly NoiraWebBridge _webBridge = new NoiraWebBridge();
        private Uri _webAppSource = new Uri(PackagedWebAppUrl);
        private bool _webViewInitialized;

        public MainPage()
        {
            InitializeComponent();
            Loaded += MainPage_OnLoaded;
        }

        public void NavigateHome()
        {
            // Kept only so legacy native pages still compile while the spike entry point is WebView-only.
        }

        private async void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainPage_OnLoaded;
            await InitializeWebViewAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            if (_webViewInitialized)
            {
                return;
            }

            _webViewInitialized = true;
            await ShellWebView.EnsureCoreWebView2Async();
            var core = ShellWebView.CoreWebView2;
            if (core == null)
            {
                return;
            }

            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.SetVirtualHostNameToFolderMapping(
                PackagedWebAppHostName,
                PackagedWebAppFolderName,
                CoreWebView2HostResourceAccessKind.Deny);
            core.WebMessageReceived += ShellWebView_OnWebMessageReceived;

            _webAppSource = await WebViewSourceResolver.ResolveAsync(new Uri(PackagedWebAppUrl));
            ShellWebView.Source = _webAppSource;
            ShellWebView.Focus(FocusState.Programmatic);
        }

        private async void ShellWebView_OnWebMessageReceived(
            CoreWebView2 sender,
            CoreWebView2WebMessageReceivedEventArgs args)
        {
            var result = await _webBridge.HandleAsync(
                args.Source,
                _webAppSource,
                args.WebMessageAsJson);
            sender.PostWebMessageAsJson(result.ResponseJson);

            if (result.PlaybackRequest != null)
            {
                Frame.Navigate(typeof(PlaybackPage), result.PlaybackRequest);
            }
        }
    }
}
