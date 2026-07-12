using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using NoiraPlayer.App.Input;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Services;
using NoiraPlayer.App.Web;
using NoiraPlayer.App.Views;
using NoiraPlayer.Core.Diagnostics;
using NoiraPlayer.Core.Input;
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
        private const string ActivatedHomeMessage =
            "{\"type\":\"host.lifecycle\",\"event\":\"activated-home\"}";
        private const int PlaybackReturnNotificationMaxAttempts = 5;
        private readonly NoiraWebBridge _webBridge = new NoiraWebBridge();
        private IDisposable? _browseInputRegistration;
        private Uri _webAppSource = new Uri(PackagedWebAppUrl);
        private bool _playbackLaunchInFlight;
        private bool _playbackNavigationPending;
        private bool _playbackReturnNotificationInFlight;
        private bool _playbackReturnObserved;
        private bool _playbackTeardownCompleted;
        private bool _webViewInitialized;
        private bool _webInputReady;
        private bool _homeNavigationRequested;

        public MainPage()
        {
            InitializeComponent();
            PlaybackDiagnosticsLog.WriteBuildMarker(typeof(MainPage));
            NavigationCacheMode = NavigationCacheMode.Required;
            Loaded += MainPage_OnLoaded;
            Unloaded += MainPage_OnUnloaded;
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
            _homeNavigationRequested = true;
            TryPostHomeNavigationRequested();
        }

        private async void MainPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            EnsureBrowseInputRegistration();
            await InitializeWebViewAsync();
            TryPostHomeNavigationRequested();
            TryPostPlaybackReturned();
        }

        private void MainPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            MarkWebInputNotReady();
            _browseInputRegistration?.Dispose();
            _browseInputRegistration = null;
            InputDiagnosticsLog.Write("input context left BrowseWeb");
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
            core.NavigationStarting += ShellWebView_OnNavigationStarting;

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

        private void EnsureBrowseInputRegistration()
        {
            if (_browseInputRegistration != null)
            {
                return;
            }

            _browseInputRegistration = ((App)Application.Current).InputRouter.Register(
                InputContext.BrowseWeb,
                PostBrowseInput);
            InputDiagnosticsLog.Write("input context entered BrowseWeb");
        }

        private void PostBrowseInput(InputEnvelope input)
        {
            var core = ShellWebView.CoreWebView2;
            if (!_webInputReady || core == null)
            {
                return;
            }

            try
            {
                core.PostWebMessageAsJson(WebHostInputMessageSerializer.Serialize(input));
            }
            catch (Exception error)
            {
                MarkWebInputNotReady();
                InputDiagnosticsLog.Write(
                    "web input transport failed type=" + error.GetType().FullName +
                    " hresult=0x" + error.HResult.ToString("X8"));
            }
        }

        private void ShellWebView_OnNavigationStarting(
            CoreWebView2 sender,
            CoreWebView2NavigationStartingEventArgs args)
        {
            MarkWebInputNotReady();
            InputDiagnosticsLog.Write("web input transport not-ready");
        }

        private void MarkWebInputNotReady()
        {
            _webInputReady = false;
            ((App)Application.Current).InputRouter.ResetInputState();
        }

        private void TryPostHomeNavigationRequested()
        {
            if (!_homeNavigationRequested)
            {
                return;
            }

            var core = ShellWebView.CoreWebView2;
            if (core == null)
            {
                return;
            }

            core.PostWebMessageAsJson(ActivatedHomeMessage);
            _homeNavigationRequested = false;
            ShellWebView.Focus(FocusState.Programmatic);
        }

        private async void ShellWebView_OnWebMessageReceived(
            CoreWebView2 sender,
            CoreWebView2WebMessageReceivedEventArgs args)
        {
            if (TryHandleHostControlMessage(args.Source, args.WebMessageAsJson))
            {
                return;
            }

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

        private bool TryHandleHostControlMessage(string source, string messageJson)
        {
            if (!WebHostControlMessage.TryParse(messageJson, out var message))
            {
                return false;
            }

            if (!NoiraWebBridge.IsAllowedSource(source, _webAppSource))
            {
                return true;
            }

            if (message.Command == WebHostControlCommand.Ready)
            {
                if (message.InputVersion != 1)
                {
                    InputDiagnosticsLog.Write("web input transport rejected version");
                    return true;
                }

                ((App)Application.Current).InputRouter.ResetInputState();
                _webInputReady = true;
                InputDiagnosticsLog.Write("web input transport ready");
                return true;
            }

            if (message.Command == WebHostControlCommand.NativeBack)
            {
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
                else
                {
                    InputDiagnosticsLog.Write("native back reached root");
                }
                return true;
            }

            return false;
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
                    command.Route != "quality-run")
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
