using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Playback;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using CorePlaybackState = NextGenEmby.Core.Playback.PlaybackState;

namespace NextGenEmby.App.Views
{
    public sealed partial class PlaybackPage : Page
    {
        private const string DemoItemId = "manual-direct-stream";
        private static readonly bool UseNativePlaybackBackend = true;
        private static readonly TimeSpan SeekBackStep = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SeekForwardStep = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(10);

        private readonly IPlaybackBackend _backend;
        private readonly IDisposable? _disposableBackend;
        private readonly PlaybackOrchestrator _orchestrator;
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private readonly DispatcherTimer _progressTimer;
        private HttpClient? _httpClient;
        private EmbyApiClient? _embyClient;
        private EmbySession? _session;
        private PlaybackLaunchRequest? _launchRequest;
        private string _currentItemName = "";
        private long _lastPositionTicks;
        private bool _hasPlaybackContext;
        private bool _infoVisible;
        private bool _reportInProgress;
        private bool _updatingStreamControls;

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
            _progressTimer = new DispatcherTimer
            {
                Interval = ProgressInterval
            };
            _progressTimer.Tick += ProgressTimer_OnTick;
            Unloaded += PlaybackPage_OnUnloaded;

            UpdateStatus(CorePlaybackState.Stopped);
            UpdateControlStates();
            UpdateStreamControlStates();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _launchRequest = e.Parameter as PlaybackLaunchRequest;
            if (_launchRequest == null)
            {
                return;
            }

            _currentItemName = _launchRequest.ItemName;
            StreamUrlBox.Text = string.IsNullOrWhiteSpace(_currentItemName)
                ? _launchRequest.ItemId
                : _currentItemName;
            StreamUrlBox.IsEnabled = false;
            _ = RunPlaybackCommandAsync(() => StartItemPlaybackAsync(_launchRequest));
        }

        private async void Start_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(StartPlaybackAsync);
        }

        private async void Pause_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(PausePlaybackAsync);
        }

        private async void Resume_OnClick(object sender, RoutedEventArgs e)
        {
            await RunPlaybackCommandAsync(ResumePlaybackAsync);
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
                if (args.PositionTicks.HasValue)
                {
                    _lastPositionTicks = Math.Max(0, args.PositionTicks.Value);
                }

                if (args.State == CorePlaybackState.Failed || args.State == CorePlaybackState.Stopped)
                {
                    _progressTimer.Stop();
                }

                _hasPlaybackContext = args.State != CorePlaybackState.Failed &&
                    args.State != CorePlaybackState.Stopped &&
                    (_hasPlaybackContext || args.State == CorePlaybackState.Opening);

                UpdateStatus(args.State, args.Message);
                UpdateControlStates();
                UpdateStreamControlStates();
                if (_infoVisible)
                {
                    UpdateInfo();
                }
            });
        }

        private async void PlaybackPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            _orchestrator.StateChanged -= Orchestrator_OnStateChanged;
            _progressTimer.Stop();
            _progressTimer.Tick -= ProgressTimer_OnTick;
            try
            {
                await ReportProgressAsync(PlaybackProgressEvent.StateChange);
                await _orchestrator.StopAsync();
            }
            finally
            {
                _httpClient?.Dispose();
                _disposableBackend?.Dispose();
            }
        }

        private async void SourceBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingStreamControls)
            {
                return;
            }

            var option = SourceBox.SelectedItem as SourceOption;
            if (option == null)
            {
                return;
            }

            await RunPlaybackCommandAsync(async () =>
            {
                await _orchestrator.SwitchMediaSourceAsync(option.Id);
                await ReportProgressAsync(PlaybackProgressEvent.QualityChange);
                UpdateStreamControls();
            });
        }

        private async void AudioStreamBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingStreamControls)
            {
                return;
            }

            var option = AudioStreamBox.SelectedItem as StreamOption;
            if (option == null)
            {
                return;
            }

            await RunPlaybackCommandAsync(async () =>
            {
                await _orchestrator.SwitchAudioStreamAsync(option.StreamIndex);
                await ReportProgressAsync(PlaybackProgressEvent.AudioTrackChange);
                UpdateStreamControls();
            });
        }

        private async void SubtitleStreamBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingStreamControls)
            {
                return;
            }

            var option = SubtitleStreamBox.SelectedItem as StreamOption;
            if (option == null)
            {
                return;
            }

            await RunPlaybackCommandAsync(async () =>
            {
                await _orchestrator.SwitchSubtitleStreamAsync(option.StreamIndex);
                await ReportProgressAsync(PlaybackProgressEvent.SubtitleTrackChange);
                UpdateStreamControls();
            });
        }

        private async Task StartPlaybackAsync()
        {
            if (_launchRequest != null)
            {
                await StartItemPlaybackAsync(_launchRequest);
                return;
            }

            await StartManualPlaybackAsync();
        }

        private async Task StartManualPlaybackAsync()
        {
            var source = CreateManualSource();
            await _orchestrator.StartAsync(DemoItemId, new[] { source }, 0);
            _lastPositionTicks = 0;
            _hasPlaybackContext = _orchestrator.CurrentDescriptor != null;
            _progressTimer.Stop();
            _currentItemName = "";
            UpdateStatus(_orchestrator.State);
            UpdateControlStates();
            UpdateStreamControls();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task StartItemPlaybackAsync(PlaybackLaunchRequest request)
        {
            await EnsureEmbyClientAsync();
            if (_embyClient == null || _session == null)
            {
                throw new InvalidOperationException("Sign in before playback.");
            }

            UpdateStatus(CorePlaybackState.Opening, "Loading media sources");
            var sources = await _embyClient.GetPlaybackInfoAsync(_session, request.ItemId);
            if (sources.Count == 0)
            {
                throw new InvalidOperationException("No playable media source was returned by Emby.");
            }

            await _orchestrator.StartAsync(request.ItemId, sources, request.StartPositionTicks);
            _lastPositionTicks = request.StartPositionTicks;
            _hasPlaybackContext = _orchestrator.CurrentDescriptor != null;
            _currentItemName = request.ItemName;
            StreamUrlBox.Text = string.IsNullOrWhiteSpace(_currentItemName)
                ? request.ItemId
                : _currentItemName;
            _progressTimer.Start();
            await ReportProgressAsync(PlaybackProgressEvent.StateChange);
            UpdateStatus(_orchestrator.State);
            UpdateControlStates();
            UpdateStreamControls();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task StopPlaybackAsync()
        {
            await ReportProgressAsync(PlaybackProgressEvent.StateChange);
            await _orchestrator.StopAsync();
            _progressTimer.Stop();
            _lastPositionTicks = 0;
            _hasPlaybackContext = false;
            UpdateStatus(CorePlaybackState.Stopped);
            UpdateControlStates();
            UpdateStreamControls();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task PausePlaybackAsync()
        {
            await _orchestrator.PauseAsync();
            await ReportProgressAsync(PlaybackProgressEvent.Pause);
        }

        private async Task ResumePlaybackAsync()
        {
            await _orchestrator.ResumeAsync();
            await ReportProgressAsync(PlaybackProgressEvent.Unpause);
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
            _lastPositionTicks = target.Ticks;
            await ReportProgressAsync(PlaybackProgressEvent.TimeUpdate);
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
                UpdateStreamControlStates();
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

        private void UpdateStreamControls()
        {
            _updatingStreamControls = true;
            try
            {
                SourceBox.Items.Clear();
                AudioStreamBox.Items.Clear();
                SubtitleStreamBox.Items.Clear();

                var descriptor = _orchestrator.CurrentDescriptor;
                if (descriptor == null)
                {
                    UpdateStreamControlStates();
                    return;
                }

                foreach (var source in descriptor.AvailableSources)
                {
                    var option = new SourceOption(source.Id, CreateSourceLabel(source));
                    SourceBox.Items.Add(option);
                    if (string.Equals(source.Id, descriptor.MediaSource.Id, StringComparison.Ordinal))
                    {
                        SourceBox.SelectedItem = option;
                    }
                }

                AddAudioOptions(descriptor);
                AddSubtitleOptions(descriptor);
                UpdateStreamControlStates();
            }
            finally
            {
                _updatingStreamControls = false;
            }
        }

        private void AddAudioOptions(PlaybackDescriptor descriptor)
        {
            var defaultOption = new StreamOption(null, "Default");
            AudioStreamBox.Items.Add(defaultOption);
            AudioStreamBox.SelectedItem = defaultOption;

            foreach (var stream in descriptor.MediaSource.AudioStreams)
            {
                var option = new StreamOption(stream.Index, CreateStreamLabel(stream));
                AudioStreamBox.Items.Add(option);
                if (descriptor.AudioStreamIndex == stream.Index)
                {
                    AudioStreamBox.SelectedItem = option;
                }
            }
        }

        private void AddSubtitleOptions(PlaybackDescriptor descriptor)
        {
            var offOption = new StreamOption(null, "Off");
            SubtitleStreamBox.Items.Add(offOption);
            SubtitleStreamBox.SelectedItem = offOption;

            foreach (var stream in descriptor.MediaSource.SubtitleStreams)
            {
                var option = new StreamOption(stream.Index, CreateStreamLabel(stream));
                SubtitleStreamBox.Items.Add(option);
                if (descriptor.SubtitleStreamIndex == stream.Index)
                {
                    SubtitleStreamBox.SelectedItem = option;
                }
            }
        }

        private void UpdateStreamControlStates()
        {
            var state = _orchestrator.State;
            var hasActivePlayback = _hasPlaybackContext &&
                state != CorePlaybackState.Failed &&
                state != CorePlaybackState.Stopped;

            SourceBox.IsEnabled = hasActivePlayback && SourceBox.Items.Count > 1;
            AudioStreamBox.IsEnabled = hasActivePlayback && AudioStreamBox.Items.Count > 1;
            SubtitleStreamBox.IsEnabled = hasActivePlayback && SubtitleStreamBox.Items.Count > 1;
        }

        private async Task EnsureEmbyClientAsync()
        {
            if (_embyClient != null && _session != null)
            {
                return;
            }

            _session = await _sessionStore.LoadAsync();
            if (_session == null)
            {
                return;
            }

            _httpClient = new HttpClient();
            _embyClient = EmbyClientFactory.Create(_httpClient, _session);
        }

        private async void ProgressTimer_OnTick(object sender, object e)
        {
            await ReportProgressAsync(PlaybackProgressEvent.TimeUpdate);
        }

        private async Task ReportProgressAsync(PlaybackProgressEvent eventName)
        {
            if (_reportInProgress ||
                _embyClient == null ||
                _session == null ||
                _orchestrator.CurrentDescriptor == null)
            {
                return;
            }

            try
            {
                _reportInProgress = true;
                await _embyClient.ReportProgressAsync(
                    _session,
                    _orchestrator.CreateProgressRequest(eventName));
            }
            catch
            {
            }
            finally
            {
                _reportInProgress = false;
            }
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

            StartButton.IsEnabled = _launchRequest != null || IsSupportedDirectStreamUrl(StreamUrlBox.Text);
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
            var position = TimeSpan.FromTicks(GetCurrentPositionTicks());

            if (descriptor == null || source == null)
            {
                InfoBlock.Text =
                    "State: " + _orchestrator.State + Environment.NewLine +
                    "Position: " + FormatPosition(position);
                return;
            }

            InfoBlock.Text =
                "State: " + _orchestrator.State + Environment.NewLine +
                "Item: " + (string.IsNullOrWhiteSpace(_currentItemName) ? descriptor.ItemId : _currentItemName) + Environment.NewLine +
                "Source: " + source.Name + Environment.NewLine +
                "Audio: " + CreateSelectedStreamLabel(source, descriptor.AudioStreamIndex, EmbyStreamKind.Audio, "Default") + Environment.NewLine +
                "Subtitles: " + CreateSelectedStreamLabel(source, descriptor.SubtitleStreamIndex, EmbyStreamKind.Subtitle, "Off") + Environment.NewLine +
                "Position: " + FormatPosition(position) + Environment.NewLine +
                "URL: " + source.DirectStreamUrl;
        }

        private static string CreateSourceLabel(EmbyMediaSource source)
        {
            var label = string.IsNullOrWhiteSpace(source.Name) ? source.Id : source.Name;
            if (source.Width > 0 && source.Height > 0)
            {
                label += " · " + source.Width + "x" + source.Height;
            }

            if (source.IsHdr)
            {
                label += " · HDR";
            }

            return label;
        }

        private static string CreateStreamLabel(EmbyMediaStream stream)
        {
            if (!string.IsNullOrWhiteSpace(stream.DisplayTitle))
            {
                return stream.DisplayTitle;
            }

            var label = string.IsNullOrWhiteSpace(stream.Language) ? "Track " + stream.Index : stream.Language;
            if (!string.IsNullOrWhiteSpace(stream.Codec))
            {
                label += " · " + stream.Codec;
            }

            if (!string.IsNullOrWhiteSpace(stream.ChannelLayout))
            {
                label += " · " + stream.ChannelLayout;
            }

            if (stream.IsExternal)
            {
                label += " · External";
            }

            return label;
        }

        private static string CreateSelectedStreamLabel(
            EmbyMediaSource source,
            int? streamIndex,
            EmbyStreamKind streamKind,
            string fallback)
        {
            if (!streamIndex.HasValue)
            {
                return fallback;
            }

            var stream = source.Streams.FirstOrDefault(candidate =>
                candidate.Kind == streamKind && candidate.Index == streamIndex.Value);
            return stream == null ? fallback : CreateStreamLabel(stream);
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

        private long GetCurrentPositionTicks()
        {
            return Math.Max(0, Math.Max(_lastPositionTicks, _backend.CurrentPositionTicks));
        }

        private static string FormatPosition(TimeSpan position)
        {
            return string.Format(
                "{0:D2}:{1:D2}:{2:D2}",
                (int)position.TotalHours,
                position.Minutes,
                position.Seconds);
        }

        private sealed class SourceOption
        {
            public SourceOption(string id, string label)
            {
                Id = id ?? "";
                Label = label ?? "";
            }

            public string Id { get; }

            public string Label { get; }

            public override string ToString()
            {
                return Label;
            }
        }

        private sealed class StreamOption
        {
            public StreamOption(int? streamIndex, string label)
            {
                StreamIndex = streamIndex;
                Label = label ?? "";
            }

            public int? StreamIndex { get; }

            public string Label { get; }

            public override string ToString()
            {
                return Label;
            }
        }
    }
}
