using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Services;
using NoiraPlayer.App.Web;
using NoiraPlayer.App.Views;
using NoiraPlayer.Core.Diagnostics;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace NoiraPlayer.App
{
    public sealed partial class MainPage : Page
    {
#if DEBUG
        private const string DevelopmentCommandFileName = "dev-command.json";
#endif
        private const string PackagedWebAppHostName = "app.noira.local";
        private const string PackagedWebAppFolderName = "WebCode";
        private const string PackagedWebAppUrl = "https://app.noira.local/index.html";
        private const string PlaybackReturnedMessage =
            "{\"type\":\"host.lifecycle\",\"event\":\"playback-returned\"}";
        private const int PlaybackReturnNotificationMaxAttempts = 5;
        private readonly NoiraWebBridge _webBridge = new NoiraWebBridge();
        private Uri _webAppSource = new Uri(PackagedWebAppUrl);
        private bool _playbackLaunchInFlight;
        private bool _playbackNavigationPending;
        private bool _playbackReturnNotificationInFlight;
        private bool _playbackReturnObserved;
        private bool _playbackTeardownCompleted;
        private bool _webViewInitialized;

        public MainPage()
        {
            InitializeComponent();
            PlaybackDiagnosticsLog.WriteLine(
                "Web shell module=" + typeof(MainPage).Module.ModuleVersionId.ToString("D"));
            NavigationCacheMode = NavigationCacheMode.Required;
            Loaded += MainPage_OnLoaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (!_playbackNavigationPending)
            {
                return;
            }

            _playbackReturnObserved = true;
            TryPostPlaybackReturned();
        }

        public void NavigateHome()
        {
            // Kept only so legacy native pages still compile while the spike entry point is WebView-only.
        }

        private async void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
            TryPostPlaybackReturned();
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

#if DEBUG
            if (await TryRunDevelopmentPlaybackCommandAsync())
            {
                return;
            }
#endif

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
            if (result.PlaybackRequest == null)
            {
                sender.PostWebMessageAsJson(result.ResponseJson);
                return;
            }

            if (_playbackLaunchInFlight || _playbackNavigationPending)
            {
                sender.PostWebMessageAsJson(result.PlaybackNavigationFailedResponseJson);
                return;
            }

            _playbackLaunchInFlight = true;
            _playbackReturnObserved = false;
            _playbackTeardownCompleted = false;
            PlaybackPage.TeardownCompleted += PlaybackPage_OnTeardownCompleted;
            try
            {
                await Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () =>
                    {
                        _playbackNavigationPending = Frame.Navigate(
                            typeof(PlaybackPage),
                            result.PlaybackRequest);
                    });
            }
            catch (Exception ex)
            {
                PlaybackDiagnosticsLog.WriteLine(
                    "Native playback navigation failed stage=frame.navigate type=" +
                    ex.GetType().FullName +
                    " hresult=0x" + ex.HResult.ToString("X8", CultureInfo.InvariantCulture));
                _playbackNavigationPending = false;
            }
            finally
            {
                _playbackLaunchInFlight = false;
            }

            if (!_playbackNavigationPending)
            {
                PlaybackPage.TeardownCompleted -= PlaybackPage_OnTeardownCompleted;
            }

            sender.PostWebMessageAsJson(
                _playbackNavigationPending
                    ? result.ResponseJson
                    : result.PlaybackNavigationFailedResponseJson);
        }

        private void PlaybackPage_OnTeardownCompleted(object? sender, EventArgs e)
        {
            PlaybackPage.TeardownCompleted -= PlaybackPage_OnTeardownCompleted;
            _playbackTeardownCompleted = true;
            TryPostPlaybackReturned();
        }

        private async void TryPostPlaybackReturned()
        {
            if (
                !_playbackNavigationPending ||
                !_playbackReturnObserved ||
                !_playbackTeardownCompleted ||
                _playbackReturnNotificationInFlight)
            {
                return;
            }

            _playbackReturnNotificationInFlight = true;
            try
            {
                for (
                    var attempt = 0;
                    attempt < PlaybackReturnNotificationMaxAttempts && _playbackNavigationPending;
                    attempt++)
                {
                    try
                    {
                        await ShellWebView.EnsureCoreWebView2Async();
                        var core = ShellWebView.CoreWebView2;
                        if (core == null)
                        {
                            throw new InvalidOperationException(
                                "CoreWebView2 is unavailable for playback return notification.");
                        }

                        core.PostWebMessageAsJson(PlaybackReturnedMessage);
                        _playbackNavigationPending = false;
                        _playbackReturnObserved = false;
                        _playbackTeardownCompleted = false;
                        ShellWebView.Focus(FocusState.Programmatic);
                        return;
                    }
                    catch (Exception ex)
                    {
                        PlaybackDiagnosticsLog.WriteLine(
                            "Playback return notification attempt " + (attempt + 1) + " failed " +
                            ex.GetType().FullName + " " + ex.Message);
                    }

                    if (
                        attempt + 1 < PlaybackReturnNotificationMaxAttempts &&
                        _playbackNavigationPending)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100 * (1 << attempt)));
                    }
                }
            }
            finally
            {
                _playbackReturnNotificationInFlight = false;
            }
        }

#if DEBUG
        private async Task<bool> TryRunDevelopmentPlaybackCommandAsync()
        {
            try
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(DevelopmentCommandFileName);
                var file = item as StorageFile;
                if (file == null)
                {
                    return false;
                }

                var json = await FileIO.ReadTextAsync(file);
                if (!DevelopmentNavigationCommand.TryParseJson(json, out var command, out _) ||
                    command == null ||
                    command.Route != "quality-run" ||
                    string.IsNullOrWhiteSpace(command.StreamUrl))
                {
                    return false;
                }

                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                Frame.Navigate(
                    typeof(PlaybackPage),
                    PlaybackLaunchRequest.FromDevelopmentQualityRun(command, DateTimeOffset.UtcNow));
                return true;
            }
            catch (Exception ex)
            {
                PlaybackDiagnosticsLog.WriteLine(
                    "Development playback command failed " + ex.GetType().FullName + " " + ex.Message);
                return false;
            }
        }
#endif
    }
}
