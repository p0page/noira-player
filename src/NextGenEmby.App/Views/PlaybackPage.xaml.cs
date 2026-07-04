using System;
using System.Threading.Tasks;
using NextGenEmby.App.Playback;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using CorePlaybackState = NextGenEmby.Core.Playback.PlaybackState;

namespace NextGenEmby.App.Views
{
    public sealed partial class PlaybackPage : Page
    {
        private const string DemoItemId = "manual-direct-stream";
        private static readonly bool UseNativePlaybackBackend = true;
        private static readonly TimeSpan SeekBackStep = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SeekForwardStep = TimeSpan.FromSeconds(30);

        private readonly IPlaybackBackend _backend;
        private readonly IDisposable? _disposableBackend;
        private readonly PlaybackOrchestrator _orchestrator;
        private bool _hasPlaybackContext;
        private bool _infoVisible;

        public PlaybackPage()
        {
            InitializeComponent();

            if (UseNativePlaybackBackend)
            {
                var nativeEngine = new WinRtNativePlaybackEngine(new NextGenEmby.Native.NativePlaybackEngine());
                nativeEngine.AttachSurface(NativeSurface);
                _backend = new NativeDirectXPlaybackBackend(nativeEngine);
                NativeSurface.Visibility = Visibility.Visible;
                PlayerElement.Visibility = Visibility.Collapsed;
            }
            else
            {
                var systemBackend = new SystemMediaPlaybackBackend(PlayerElement);
                _backend = systemBackend;
                _disposableBackend = systemBackend;
                NativeSurface.Visibility = Visibility.Collapsed;
                PlayerElement.Visibility = Visibility.Visible;
            }

            _orchestrator = new PlaybackOrchestrator(_backend);
            _orchestrator.StateChanged += Orchestrator_OnStateChanged;
            Unloaded += PlaybackPage_OnUnloaded;

            UpdateStatus(CorePlaybackState.Stopped);
            UpdateControlStates();
        }

        private async void Start_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(StartPlaybackAsync);
        }

        private async void Pause_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(() => _orchestrator.PauseAsync());
        }

        private async void Resume_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(() => _orchestrator.ResumeAsync());
        }

        private async void Stop_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(StopPlaybackAsync);
        }

        private async void SeekBack_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(() => SeekRelativeAsync(-SeekBackStep));
        }

        private async void SeekForward_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(() => SeekRelativeAsync(SeekForwardStep));
        }

        private void Info_OnClick(object sender, RoutedEventArgs e)
        {
            _infoVisible = !_infoVisible;
            InfoPanel.Visibility = _infoVisible ? Visibility.Visible : Visibility.Collapsed;
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private void StreamUrlBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateControlStates();
        }

        private async void Orchestrator_OnStateChanged(object? sender, PlaybackStateChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _hasPlaybackContext = args.State != CorePlaybackState.Failed &&
                    args.State != CorePlaybackState.Stopped &&
                    (_hasPlaybackContext || args.State == CorePlaybackState.Opening);

                UpdateStatus(args.State, args.Message);
                UpdateControlStates();
                if (_infoVisible)
                {
                    UpdateInfo();
                }
            });
        }

        private async void PlaybackPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _orchestrator.StateChanged -= Orchestrator_OnStateChanged;
            try
            {
                await _orchestrator.StopAsync();
            }
            finally
            {
                _disposableBackend?.Dispose();
            }
        }

        private async Task StartPlaybackAsync()
        {
            var source = CreateManualSource();
            await _orchestrator.StartAsync(DemoItemId, new[] { source }, 0);
            _hasPlaybackContext = _orchestrator.CurrentDescriptor != null;
            UpdateStatus(_orchestrator.State);
            UpdateControlStates();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task StopPlaybackAsync()
        {
            await _orchestrator.StopAsync();
            _hasPlaybackContext = false;
            UpdateStatus(CorePlaybackState.Stopped);
            UpdateControlStates();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task SeekRelativeAsync(TimeSpan delta)
        {
            var current = TimeSpan.FromTicks(Math.Max(0, _backend.CurrentPositionTicks));
            var target = current + delta;
            if (target < TimeSpan.Zero)
            {
                target = TimeSpan.Zero;
            }

            await _orchestrator.SeekAsync(target.Ticks);
            UpdateStatus(_orchestrator.State, "Position " + FormatPosition(target));
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task RunPlaybackCommandAsync(Func<Task> command)
        {
            try
            {
                await command();
            }
            catch (Exception ex)
            {
                _hasPlaybackContext = _orchestrator.CurrentDescriptor != null;
                UpdateStatus(CorePlaybackState.Failed, ex.Message);
                UpdateControlStates();
                if (_infoVisible)
                {
                    UpdateInfo();
                }
            }
        }

        private EmbyMediaSource CreateManualSource()
        {
            var source = new EmbyMediaSource
            {
                Id = "manual-url",
                Name = "Manual Direct Stream",
                DirectStreamUrl = StreamUrlBox.Text.Trim()
            };
            source.Streams.Add(new EmbyMediaStream
            {
                Index = 0,
                Kind = EmbyStreamKind.Video,
                DisplayTitle = UseNativePlaybackBackend ? "Native direct stream" : "System player stream"
            });

            return source;
        }

        private void UpdateStatus(CorePlaybackState state, string message = "")
        {
            StatusBlock.Text = string.IsNullOrWhiteSpace(message)
                ? state.ToString()
                : state + " - " + message;
        }

        private void UpdateControlStates()
        {
            var state = _orchestrator.State;
            var hasActivePlayback = _hasPlaybackContext &&
                state != CorePlaybackState.Failed &&
                state != CorePlaybackState.Stopped;

            StartButton.IsEnabled = IsSupportedDirectStreamUrl(StreamUrlBox.Text);
            PauseButton.IsEnabled = hasActivePlayback && state != CorePlaybackState.Paused;
            ResumeButton.IsEnabled = hasActivePlayback && state == CorePlaybackState.Paused;
            StopButton.IsEnabled = hasActivePlayback;
            SeekBackButton.IsEnabled = hasActivePlayback;
            SeekForwardButton.IsEnabled = hasActivePlayback;
            InfoButton.IsEnabled = true;
        }

        private void UpdateInfo()
        {
            var descriptor = _orchestrator.CurrentDescriptor;
            var source = descriptor?.MediaSource;
            var position = TimeSpan.FromTicks(Math.Max(0, _backend.CurrentPositionTicks));

            if (descriptor == null || source == null)
            {
                InfoBlock.Text =
                    "State: " + _orchestrator.State + Environment.NewLine +
                    "Position: " + FormatPosition(position);
                return;
            }

            InfoBlock.Text =
                "State: " + _orchestrator.State + Environment.NewLine +
                "Item: " + descriptor.ItemId + Environment.NewLine +
                "Source: " + source.Name + Environment.NewLine +
                "Position: " + FormatPosition(position) + Environment.NewLine +
                "URL: " + source.DirectStreamUrl;
        }

        private static bool IsSupportedDirectStreamUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static string FormatPosition(TimeSpan position)
        {
            return string.Format(
                "{0:D2}:{1:D2}:{2:D2}",
                (int)position.TotalHours,
                position.Minutes,
                position.Seconds);
        }
    }
}
