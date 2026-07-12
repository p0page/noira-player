using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Playback;
using NoiraPlayer.App.Services;
using NoiraPlayer.App.Storage;
using NoiraPlayer.Core.Diagnostics;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Input;
using NoiraPlayer.Core.Playback;
using NoiraPlayer.Core.PlaybackQuality;
using Windows.Media.Protection;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using CorePlaybackState = NoiraPlayer.Core.Playback.PlaybackState;

namespace NoiraPlayer.App.Views
{
    public sealed partial class PlaybackPage : Page
    {
        public static event EventHandler? TeardownCompleted;

        private const string DemoItemId = "manual-direct-stream";
        private static readonly bool UseNativePlaybackBackend = true;
        private static readonly TimeSpan SeekBackStep = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SeekForwardStep = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromSeconds(10);

        private readonly IPlaybackBackend _backend;
        private readonly IDisposable? _disposableBackend;
        private readonly PlaybackOrchestrator _orchestrator;
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private readonly PlaybackPreferenceStore _playbackPreferences = new PlaybackPreferenceStore();
        private readonly TaskCompletionSource<bool> _nativeSurfaceReadySource = new TaskCompletionSource<bool>();
        private readonly SeekPreviewSession _seekPreview = new SeekPreviewSession(TimeSpan.FromSeconds(1.8), TimeSpan.FromSeconds(5), 0.55);
        private readonly DispatcherTimer _progressTimer;
        private readonly DispatcherTimer _overlayTimer;
        private readonly DispatcherTimer _seekPreviewTimer;
        private WinRtNativePlaybackEngine? _nativeEngine;
        private HttpClient? _httpClient;
        private EmbyApiClient? _embyClient;
        private EmbySession? _session;
        private PlaybackLaunchRequest? _launchRequest;
        private string _currentItemName = "";
        private long _lastPositionTicks;
        private long _durationTicks;
        private bool _hasPlaybackContext;
        private bool _infoVisible;
        private bool _reportInProgress;
        private bool _stopReportInProgress;
        private bool _playbackStoppedReported;
        private bool _updatingStreamControls;
        private bool _updatingProgressSlider;
        private bool _overlayVisible;
        private bool _moreVisible;
        private bool _keyHandlerAttached;
        private IDisposable? _inputRegistration;
        private bool _playbackCommandInFlight;
        private PlaybackMoreDrawerFocusTarget? _moreDrawerFocusTarget;
        private PlaybackTransportFocusTarget? _transportFocusTarget;
        private VirtualKey? _handledMoreDrawerComboBoxDirectionalKey;
        private DateTimeOffset _handledMoreDrawerComboBoxDirectionalKeyAt;
        private double _nativeSurfaceAttachedWidth;
        private double _nativeSurfaceAttachedHeight;
        private PlaybackSessionRequest? _lastPlaybackSessionRequest;
        private ManualDirectStreamInitialFocusTarget? _pendingManualDirectStreamFocusTarget;
        private int _pendingManualDirectStreamFocusAttempts;
        private bool _manualDirectStreamPageLoaded;
        public PlaybackPage()
        {
            InitializeComponent();

            if (UseNativePlaybackBackend)
            {
                var nativeEngine = new WinRtNativePlaybackEngine(new NoiraPlayer.Native.NativePlaybackEngine());
                _nativeEngine = nativeEngine;
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
            _overlayTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _overlayTimer.Tick += OverlayTimer_OnTick;
            _seekPreviewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _seekPreviewTimer.Tick += SeekPreviewTimer_OnTick;
            NativeSurface.SizeChanged += NativeSurface_OnSizeChanged;
            Loaded += PlaybackPage_OnLoaded;
            Unloaded += PlaybackPage_OnUnloaded;

            NowPlayingBlock.Text = "Ready";
            UpdateStatus(CorePlaybackState.Stopped);
            UpdateControlStates();
            UpdateStreamControlStates();
        }

        private void PlaybackPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            _manualDirectStreamPageLoaded = true;
            PlaybackDiagnosticsLog.WriteLine(
                "Playback page loaded surface=" + NativeSurface.ActualWidth + "x" + NativeSurface.ActualHeight);
            AttachPlaybackInput();
            AttachPlaybackKeyHandler();
            TrySignalNativeSurfaceReady();
            AttachNativeSurface();
            ApplyPendingManualDirectStreamInitialFocus();
        }

        private void NativeSurface_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            TrySignalNativeSurfaceReady();
            AttachNativeSurface();
        }

        private void AttachPlaybackKeyHandler()
        {
            if (_keyHandlerAttached)
            {
                return;
            }

            Window.Current.CoreWindow.KeyDown += PlaybackPage_OnCoreWindowKeyDown;
            _keyHandlerAttached = true;
        }

        private void AttachPlaybackInput()
        {
            if (_inputRegistration != null)
            {
                return;
            }

            _inputRegistration = ((App)Application.Current).InputRouter.Register(
                InputContext.NativePlayback,
                PlaybackPage_OnGamepadInput);
        }

        private void DetachPlaybackInput()
        {
            var registration = _inputRegistration;
            _inputRegistration = null;
            registration?.Dispose();
        }

        private void DetachPlaybackKeyHandler()
        {
            if (!_keyHandlerAttached)
            {
                return;
            }

            Window.Current.CoreWindow.KeyDown -= PlaybackPage_OnCoreWindowKeyDown;
            _keyHandlerAttached = false;
        }

        private async Task EnsureNativeSurfaceReadyAsync()
        {
            if (_nativeEngine == null)
            {
                return;
            }

            if (!IsNativeSurfaceReady())
            {
                await _nativeSurfaceReadySource.Task;
            }

            AttachNativeSurface();
        }

        private void AttachNativeSurface()
        {
            if (_nativeEngine == null || !IsNativeSurfaceReady())
            {
                return;
            }

            if (Math.Abs(NativeSurface.ActualWidth - _nativeSurfaceAttachedWidth) < 0.5 &&
                Math.Abs(NativeSurface.ActualHeight - _nativeSurfaceAttachedHeight) < 0.5)
            {
                return;
            }

            _nativeEngine.AttachSurface(NativeSurface);
            _nativeSurfaceAttachedWidth = NativeSurface.ActualWidth;
            _nativeSurfaceAttachedHeight = NativeSurface.ActualHeight;
        }

        private bool IsNativeSurfaceReady()
        {
            return NativeSurface.ActualWidth > 0 && NativeSurface.ActualHeight > 0;
        }

        private void TrySignalNativeSurfaceReady()
        {
            if (IsNativeSurfaceReady())
            {
                _nativeSurfaceReadySource.TrySetResult(true);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _launchRequest = e.Parameter as PlaybackLaunchRequest;
            var manualLaunchOptions = e.Parameter as ManualDirectStreamLaunchOptions;
            PlaybackDiagnosticsLog.WriteLine(
                "Playback navigated launch=" + (_launchRequest != null) +
                " item=" + (_launchRequest == null ? "" : _launchRequest.ItemId) +
                " source=" + (_launchRequest == null ? "" : _launchRequest.MediaSourceId) +
                " direct=" + (_launchRequest != null && _launchRequest.HasDirectStreamUrl));
            if (_launchRequest == null)
            {
                NowPlayingBlock.Text = "Manual Direct Stream";
                ManualDebugPanel.Visibility = Visibility.Visible;
                StreamUrlBox.Text = manualLaunchOptions == null ? "" : manualLaunchOptions.StreamUrl;
                StreamUrlBox.IsEnabled = true;
                UpdateControlStates();
                ShowOverlay();
                QueueManualDirectStreamInitialFocus(
                    ManualDirectStreamInputPolicy.GetInitialFocusTarget(ManualStartButton.IsEnabled));
                if (manualLaunchOptions != null &&
                    manualLaunchOptions.AutoStart &&
                    ManualStartButton.IsEnabled)
                {
                    _ = RunPlaybackCommandAsync(StartManualPlaybackAsync);
                }

                return;
            }

            _currentItemName = _launchRequest.ItemName;
            NowPlayingBlock.Text = string.IsNullOrWhiteSpace(_currentItemName)
                ? GetLaunchRequestDisplayName(_launchRequest)
                : _currentItemName;
            StreamUrlBox.Text = "";
            StreamUrlBox.IsEnabled = false;
            _ = RunPlaybackCommandAsync(() => StartLaunchRequestPlaybackAsync(_launchRequest));
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
            ToggleInfoPanel();
        }

        private void ToggleInfoPanel()
        {
            ShowOverlay(true, _moreVisible);
            _infoVisible = !_infoVisible;
            InfoPanel.Visibility = _infoVisible ? Visibility.Visible : Visibility.Collapsed;
            if (_infoVisible)
            {
                UpdateInfo();
            }

            FocusMoreDrawerTarget(PlaybackMoreDrawerFocusTarget.Info);
        }

        private void StreamUrlBox_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateControlStates();
        }

        private async void StreamUrlBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            var input = e.Key == VirtualKey.Enter
                ? ManualDirectStreamInput.Accept
                : ManualDirectStreamInput.Other;
            if (!ManualDirectStreamInputPolicy.ShouldStartFromTextBox(input, ManualStartButton.IsEnabled))
            {
                return;
            }

            e.Handled = true;
            await StartManualPlaybackAsync();
        }

        private void QueueManualDirectStreamInitialFocus(ManualDirectStreamInitialFocusTarget target)
        {
            _pendingManualDirectStreamFocusTarget = target;
            _pendingManualDirectStreamFocusAttempts = 0;
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, ApplyPendingManualDirectStreamInitialFocus);
        }

        private void ApplyPendingManualDirectStreamInitialFocus()
        {
            if (!_pendingManualDirectStreamFocusTarget.HasValue)
            {
                return;
            }

            var target = _pendingManualDirectStreamFocusTarget.Value;
            if (!_manualDirectStreamPageLoaded)
            {
                PlaybackDiagnosticsLog.WriteLine(
                    "ManualDirectStream initial focus target=" + target +
                    " deferred pageLoaded=False");
                return;
            }

            var applied = FocusManualDirectStreamTarget(target);
            PlaybackDiagnosticsLog.WriteLine(
                "ManualDirectStream initial focus target=" + target +
                " applied=" + applied +
                " attempt=" + _pendingManualDirectStreamFocusAttempts);
            if (!ManualDirectStreamInputPolicy.ShouldKeepInitialFocusPending(
                applied,
                _manualDirectStreamPageLoaded,
                _pendingManualDirectStreamFocusAttempts,
                maxAttempts: 5))
            {
                _pendingManualDirectStreamFocusTarget = null;
                return;
            }

            _pendingManualDirectStreamFocusAttempts++;
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Low, ApplyPendingManualDirectStreamInitialFocus);
        }

        private bool FocusManualDirectStreamTarget(ManualDirectStreamInitialFocusTarget target)
        {
            Control control = target == ManualDirectStreamInitialFocusTarget.StartButton
                ? ManualStartButton
                : StreamUrlBox;
            return control.Focus(FocusState.Keyboard) ||
                control.Focus(FocusState.Programmatic);
        }

        private void ProgressSlider_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_updatingProgressSlider ||
                !ProgressSlider.IsEnabled ||
                !IsPlaybackSeekable())
            {
                return;
            }

            BeginOrMoveSeekPreviewTo(TimeSpan.FromSeconds(Math.Max(0, e.NewValue)).Ticks);
        }

        private void More_OnClick(object sender, RoutedEventArgs e)
        {
            if (_moreVisible)
            {
                CloseMoreDrawer();
                return;
            }

            ShowOverlay(true);
        }

        private async void PlaybackPage_OnCoreWindowKeyDown(CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            if (IsGamepadVirtualKey(args.VirtualKey))
            {
                args.Handled = true;
                return;
            }

            if (ShouldIgnoreMoreDrawerComboBoxDirectionalReplay(args.VirtualKey))
            {
                args.Handled = true;
                return;
            }

            if (args.Handled && !ShouldProcessHandledPlaybackKey(args.VirtualKey))
            {
                return;
            }

            args.Handled = await HandlePlaybackKeyAsync(args.VirtualKey, IsPreviewModifierDown(sender)) || args.Handled;
        }

        private void PlaybackPage_OnGamepadInput(InputEnvelope input)
        {
            if (input.Phase == InputPhase.Released)
            {
                return;
            }

            var key = TryMapGamepadInput(input);
            if (!key.HasValue)
            {
                return;
            }

            _ = HandleGamepadInputAsync(key.Value);
        }

        private async Task HandleGamepadInputAsync(VirtualKey key)
        {
            try
            {
                await HandlePlaybackKeyAsync(key, false);
            }
            catch (Exception error)
            {
                PlaybackDiagnosticsLog.WriteLine(
                    "Playback input consumer exception " + error.GetType().FullName);
            }
        }

        private static VirtualKey? TryMapGamepadInput(InputEnvelope input)
        {
            switch (input.Command)
            {
                case InputCommand.Accept:
                    return VirtualKey.GamepadA;
                case InputCommand.Back:
                    return VirtualKey.GamepadB;
                case InputCommand.Menu:
                    return VirtualKey.GamepadMenu;
                case InputCommand.MoveLeft:
                    return input.ControlKind == InputControlKind.LeftThumbstick
                        ? VirtualKey.GamepadLeftThumbstickLeft
                        : VirtualKey.GamepadDPadLeft;
                case InputCommand.MoveRight:
                    return input.ControlKind == InputControlKind.LeftThumbstick
                        ? VirtualKey.GamepadLeftThumbstickRight
                        : VirtualKey.GamepadDPadRight;
                case InputCommand.MoveUp:
                    return input.ControlKind == InputControlKind.LeftThumbstick
                        ? VirtualKey.GamepadLeftThumbstickUp
                        : VirtualKey.GamepadDPadUp;
                case InputCommand.MoveDown:
                    return input.ControlKind == InputControlKind.LeftThumbstick
                        ? VirtualKey.GamepadLeftThumbstickDown
                        : VirtualKey.GamepadDPadDown;
                default:
                    return null;
            }
        }

        private static bool IsGamepadVirtualKey(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.GamepadA:
                case VirtualKey.GamepadB:
                case VirtualKey.GamepadX:
                case VirtualKey.GamepadY:
                case VirtualKey.GamepadRightShoulder:
                case VirtualKey.GamepadLeftShoulder:
                case VirtualKey.GamepadLeftTrigger:
                case VirtualKey.GamepadRightTrigger:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.GamepadDPadRight:
                case VirtualKey.GamepadMenu:
                case VirtualKey.GamepadView:
                case VirtualKey.GamepadLeftThumbstickButton:
                case VirtualKey.GamepadRightThumbstickButton:
                case VirtualKey.GamepadLeftThumbstickUp:
                case VirtualKey.GamepadLeftThumbstickDown:
                case VirtualKey.GamepadLeftThumbstickRight:
                case VirtualKey.GamepadLeftThumbstickLeft:
                case VirtualKey.GamepadRightThumbstickUp:
                case VirtualKey.GamepadRightThumbstickDown:
                case VirtualKey.GamepadRightThumbstickRight:
                case VirtualKey.GamepadRightThumbstickLeft:
                    return true;
                default:
                    return false;
            }
        }

        private async Task<bool> HandlePlaybackKeyAsync(VirtualKey key, bool previewModifierDown)
        {
            if (TryHandleMoreDrawerDirectionalKey(key))
            {
                return true;
            }

            if (TryHandleKeyboardSeekPreview(key, previewModifierDown))
            {
                return true;
            }

            if (TryHandleTransportDirectionalKey(key))
            {
                return true;
            }

            if (ShouldActivateTransportControl(key))
            {
                await ActivateFocusedTransportControlAsync();
                return true;
            }

            if (ShouldLetFocusedControlHandleKey(key))
            {
                return false;
            }

            var shortcut = TryMapDesktopShortcut(key);
            if (shortcut.HasValue)
            {
                var action = PlaybackOverlayInputPolicy.Decide(
                    shortcut.Value,
                    _seekPreview.IsActive,
                    _moreVisible,
                    _overlayVisible,
                    ShouldBackExitPlaybackPage());
                if (action == PlaybackOverlayInputAction.None)
                {
                    return false;
                }

                await ApplyOverlayInputActionAsync(action);
                return true;
            }

            switch (key)
            {
                case VirtualKey.GamepadA:
                    var action = PlaybackOverlayInputPolicy.Decide(
                        PlaybackOverlayShortcut.Accept,
                        _seekPreview.IsActive,
                        _moreVisible,
                        _overlayVisible,
                        ShouldBackExitPlaybackPage());
                    if (action == PlaybackOverlayInputAction.None)
                    {
                        return false;
                    }

                    await ApplyOverlayInputActionAsync(action);
                    return true;

                case VirtualKey.GamepadB:
                    await ApplyOverlayInputActionAsync(
                        PlaybackOverlayInputPolicy.Decide(PlaybackOverlayShortcut.Cancel, _seekPreview.IsActive, _moreVisible, _overlayVisible, ShouldBackExitPlaybackPage()));
                    return true;

                case VirtualKey.GamepadMenu:
                    await ApplyOverlayInputActionAsync(
                        PlaybackOverlayInputPolicy.Decide(PlaybackOverlayShortcut.More, _seekPreview.IsActive, _moreVisible, _overlayVisible, ShouldBackExitPlaybackPage()));
                    return true;

                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.Left:
                    if (CanAcceptSeekInput())
                    {
                        ClearSeekPreview();
                        await RunPlaybackCommandAsync(() => SeekRelativeAsync(-SeekBackStep));
                    }

                    return true;

                case VirtualKey.GamepadDPadRight:
                case VirtualKey.Right:
                    if (CanAcceptSeekInput())
                    {
                        ClearSeekPreview();
                        await RunPlaybackCommandAsync(() => SeekRelativeAsync(SeekForwardStep));
                    }

                    return true;

                case VirtualKey.GamepadLeftThumbstickLeft:
                    if (CanAcceptSeekInput() && _playbackPreferences.IsThumbstickSeekPreviewEnabled())
                    {
                        BeginOrMoveSeekPreview(TimeSpan.FromSeconds(-5));
                    }

                    return true;

                case VirtualKey.GamepadLeftThumbstickRight:
                    if (CanAcceptSeekInput() && _playbackPreferences.IsThumbstickSeekPreviewEnabled())
                    {
                        BeginOrMoveSeekPreview(TimeSpan.FromSeconds(5));
                    }

                    return true;
            }

            return false;
        }

        private bool TryHandleKeyboardSeekPreview(VirtualKey key, bool previewModifierDown)
        {
            var action = PlaybackSeekPreviewKeyboardPolicy.Decide(
                MapPlaybackDirectionalInput(key),
                previewModifierDown,
                _playbackPreferences.IsThumbstickSeekPreviewEnabled(),
                _moreVisible);
            if (action == PlaybackSeekPreviewKeyboardAction.None)
            {
                return false;
            }

            if (CanAcceptSeekInput())
            {
                BeginOrMoveSeekPreview(
                    action == PlaybackSeekPreviewKeyboardAction.PreviewBackward
                        ? TimeSpan.FromSeconds(-5)
                        : TimeSpan.FromSeconds(5));
            }

            return true;
        }

        private static PlaybackDirectionalInput MapPlaybackDirectionalInput(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Left:
                    return PlaybackDirectionalInput.Left;

                case VirtualKey.Right:
                    return PlaybackDirectionalInput.Right;

                default:
                    return PlaybackDirectionalInput.Other;
            }
        }

        private static bool IsPreviewModifierDown(CoreWindow coreWindow)
        {
            return (coreWindow.GetKeyState(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
        }

        private bool TryHandleTransportDirectionalKey(VirtualKey key)
        {
            if (!ShouldProcessTransportDirectionalKey(key))
            {
                return false;
            }

            var direction = TryMapTransportFocusDirection(key);
            if (!direction.HasValue)
            {
                return false;
            }

            var currentTarget = GetFocusedTransportTarget();
            var nextTarget = currentTarget.HasValue
                ? PlaybackTransportFocusPolicy.Move(
                    currentTarget.Value,
                    direction.Value,
                    PauseButton.IsEnabled,
                    ResumeButton.IsEnabled,
                    SeekBackButton.IsEnabled,
                    SeekForwardButton.IsEnabled,
                    MoreButton.IsEnabled,
                    StopButton.IsEnabled)
                : GetDefaultTransportFocusTarget();

            FocusTransportTarget(nextTarget);
            RestartOverlayTimerIfNeeded();
            return true;
        }

        private bool ShouldActivateTransportControl(VirtualKey key)
        {
            if (!_overlayVisible ||
                _moreVisible ||
                _seekPreview.IsActive)
            {
                return false;
            }

            switch (key)
            {
                case VirtualKey.Enter:
                case VirtualKey.Space:
                case VirtualKey.GamepadA:
                    return true;

                default:
                    return false;
            }
        }

        private bool ShouldProcessTransportDirectionalKey(VirtualKey key)
        {
            return _overlayVisible &&
                !_moreVisible &&
                !_seekPreview.IsActive &&
                TryMapTransportFocusDirection(key).HasValue;
        }

        private static PlaybackTransportFocusDirection? TryMapTransportFocusDirection(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Left:
                case VirtualKey.GamepadDPadLeft:
                    return PlaybackTransportFocusDirection.Left;

                case VirtualKey.Right:
                case VirtualKey.GamepadDPadRight:
                    return PlaybackTransportFocusDirection.Right;

                default:
                    return null;
            }
        }

        private bool TryHandleMoreDrawerDirectionalKey(VirtualKey key)
        {
            if (!ShouldProcessMoreDrawerDirectionalKey(key))
            {
                return false;
            }

            var direction = TryMapMoreDrawerFocusDirection(key);
            if (!direction.HasValue)
            {
                return false;
            }

            var currentTarget = GetFocusedMoreDrawerTarget();
            var nextTarget = currentTarget.HasValue
                ? PlaybackMoreDrawerFocusPolicy.Move(
                    currentTarget.Value,
                    direction.Value,
                    SourceBox.IsEnabled,
                    AudioStreamBox.IsEnabled,
                    SubtitleStreamBox.IsEnabled)
                : PlaybackMoreDrawerFocusPolicy.GetDefaultTarget(
                    SourceBox.IsEnabled,
                    AudioStreamBox.IsEnabled,
                    SubtitleStreamBox.IsEnabled);

            FocusMoreDrawerTarget(nextTarget);
            return true;
        }

        private void MoreDrawerComboBox_OnProcessKeyboardAccelerators(UIElement sender, ProcessKeyboardAcceleratorEventArgs e)
        {
            if (!(sender is ComboBox comboBox))
            {
                return;
            }

            if (!PlaybackOverlayInputPolicy.ShouldRouteMoreDrawerComboBoxDirectionalInput(
                _moreVisible,
                _seekPreview.IsActive,
                comboBox.IsDropDownOpen,
                TryMapMoreDrawerFocusDirection(e.Key).HasValue))
            {
                return;
            }

            if (TryHandleMoreDrawerDirectionalKey(e.Key))
            {
                _handledMoreDrawerComboBoxDirectionalKey = e.Key;
                _handledMoreDrawerComboBoxDirectionalKeyAt = DateTimeOffset.UtcNow;
                e.Handled = true;
            }
        }

        private bool ShouldIgnoreMoreDrawerComboBoxDirectionalReplay(VirtualKey key)
        {
            if (!_handledMoreDrawerComboBoxDirectionalKey.HasValue)
            {
                return false;
            }

            var shouldIgnore =
                _handledMoreDrawerComboBoxDirectionalKey.Value == key &&
                DateTimeOffset.UtcNow - _handledMoreDrawerComboBoxDirectionalKeyAt < TimeSpan.FromMilliseconds(500);
            _handledMoreDrawerComboBoxDirectionalKey = null;
            return shouldIgnore;
        }

        private bool ShouldProcessHandledPlaybackKey(VirtualKey key)
        {
            if (ShouldProcessMoreDrawerDirectionalKey(key))
            {
                return true;
            }

            var shortcut = TryMapDesktopShortcut(key);
            return shortcut.HasValue &&
                PlaybackOverlayInputPolicy.ShouldRouteHandledShortcutInput(
                    shortcut.Value,
                    _seekPreview.IsActive,
                    _moreVisible,
                    IsAnyMoreDrawerComboBoxOpen(),
                    _overlayVisible);
        }

        private bool ShouldProcessMoreDrawerDirectionalKey(VirtualKey key)
        {
            return _moreVisible &&
                !IsAnyMoreDrawerComboBoxOpen() &&
                TryMapMoreDrawerFocusDirection(key).HasValue;
        }

        private static PlaybackMoreDrawerFocusDirection? TryMapMoreDrawerFocusDirection(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Down:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadLeftThumbstickDown:
                    return PlaybackMoreDrawerFocusDirection.Down;

                case VirtualKey.Up:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadLeftThumbstickUp:
                    return PlaybackMoreDrawerFocusDirection.Up;

                default:
                    return null;
            }
        }

        private bool ShouldLetFocusedControlHandleKey(VirtualKey key)
        {
            if (!PlaybackOverlayInputPolicy.ShouldRouteFocusedControlInput(_moreVisible, _seekPreview.IsActive))
            {
                return false;
            }

            switch (key)
            {
                case VirtualKey.GamepadDPadLeft:
                case VirtualKey.GamepadDPadRight:
                case VirtualKey.GamepadDPadUp:
                case VirtualKey.GamepadDPadDown:
                case VirtualKey.GamepadLeftThumbstickLeft:
                case VirtualKey.GamepadLeftThumbstickRight:
                case VirtualKey.GamepadLeftThumbstickUp:
                case VirtualKey.GamepadLeftThumbstickDown:
                    return true;

                default:
                    return false;
            }
        }

        private async void Page_OnPointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var action = PlaybackOverlayInputPolicy.Decide(
                PlaybackOverlayShortcut.Pointer,
                _seekPreview.IsActive,
                _moreVisible,
                _overlayVisible,
                ShouldBackExitPlaybackPage());
            if (action == PlaybackOverlayInputAction.None)
            {
                RestartOverlayTimerIfNeeded();
                return;
            }

            await ApplyOverlayInputActionAsync(action);
        }

        private static PlaybackOverlayShortcut? TryMapDesktopShortcut(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.Enter:
                case VirtualKey.Space:
                    return PlaybackOverlayShortcut.Accept;

                case VirtualKey.Escape:
                case VirtualKey.GoBack:
                    return PlaybackOverlayShortcut.Cancel;

                case VirtualKey.M:
                    return PlaybackOverlayShortcut.More;

                default:
                    return null;
            }
        }

        private bool ShouldBackExitPlaybackPage()
        {
            return PlaybackPageExitPolicy.ShouldBackExit(_orchestrator.State);
        }

        private async Task ApplyOverlayInputActionAsync(PlaybackOverlayInputAction action)
        {
            switch (action)
            {
                case PlaybackOverlayInputAction.ShowOverlay:
                    ShowOverlay();
                    return;

                case PlaybackOverlayInputAction.ShowMore:
                    ShowOverlay(true);
                    return;

                case PlaybackOverlayInputAction.CloseMore:
                    CloseMoreDrawer();
                    return;

                case PlaybackOverlayInputAction.HideOverlay:
                    HideOverlay();
                    return;

                case PlaybackOverlayInputAction.GoBack:
                    if (Frame != null && Frame.CanGoBack)
                    {
                        Frame.GoBack();
                    }

                    return;

                case PlaybackOverlayInputAction.ConfirmSeekPreview:
                    await CompleteSeekPreviewDecisionAsync(_seekPreview.Confirm());
                    return;

                case PlaybackOverlayInputAction.CancelSeekPreview:
                    await CompleteSeekPreviewDecisionAsync(_seekPreview.Cancel());
                    return;

                case PlaybackOverlayInputAction.ActivateFocusedControl:
                    if (!TryActivateFocusedMoreControl())
                    {
                        await ActivateFocusedTransportControlAsync();
                    }

                    return;
            }
        }

        private bool TryActivateFocusedMoreControl()
        {
            if (!_moreVisible)
            {
                return false;
            }

            var focusedTarget = GetFocusedMoreDrawerTarget();
            if (focusedTarget == PlaybackMoreDrawerFocusTarget.Source)
            {
                SourceBox.IsDropDownOpen = true;
                return true;
            }

            if (focusedTarget == PlaybackMoreDrawerFocusTarget.Audio)
            {
                AudioStreamBox.IsDropDownOpen = true;
                return true;
            }

            if (focusedTarget == PlaybackMoreDrawerFocusTarget.Subtitles)
            {
                SubtitleStreamBox.IsDropDownOpen = true;
                return true;
            }

            if (focusedTarget == PlaybackMoreDrawerFocusTarget.Info)
            {
                ToggleInfoPanel();
                return true;
            }

            return false;
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

                var shouldReportStopped = args.State == CorePlaybackState.Stopped;
                _hasPlaybackContext = args.State != CorePlaybackState.Failed &&
                    args.State != CorePlaybackState.Stopped &&
                    (_hasPlaybackContext || args.State == CorePlaybackState.Opening);

                UpdateStatus(args.State, args.Message);
                UpdateProgressSlider();
                UpdateControlStates();
                UpdateStreamControlStates();
                KeepOverlayVisibleIfPinned();
                if (_infoVisible)
                {
                    UpdateInfo();
                }

                if (shouldReportStopped)
                {
                    _ = ReportPlaybackStoppedAsync();
                }
            });
        }

        private async void PlaybackPage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachPlaybackInput();
            DetachPlaybackKeyHandler();
            await PlaybackDiagnosticsLog.WriteLineAsync("Playback page unloaded begin");
            _orchestrator.StateChanged -= Orchestrator_OnStateChanged;
            _progressTimer.Stop();
            _progressTimer.Tick -= ProgressTimer_OnTick;
            _overlayTimer.Stop();
            _overlayTimer.Tick -= OverlayTimer_OnTick;
            _seekPreviewTimer.Stop();
            _seekPreviewTimer.Tick -= SeekPreviewTimer_OnTick;
            var stoppedRequest = CreateStoppedSessionRequest();
            try
            {
                await PlaybackDiagnosticsLog.WriteLineAsync("Playback page unloaded stop begin");
                await _orchestrator.StopAsync();
                await PlaybackDiagnosticsLog.WriteLineAsync("Playback page unloaded stop end");
                await ReportPlaybackStoppedAsync(stoppedRequest);
            }
            catch (Exception ex)
            {
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "Playback page unloaded exception " + ex.GetType().FullName + " " + ex.Message);
            }
            finally
            {
                try
                {
                    _httpClient?.Dispose();
                    _disposableBackend?.Dispose();
                    await PlaybackDiagnosticsLog.WriteLineAsync("Playback page unloaded end");
                }
                finally
                {
                    TeardownCompleted?.Invoke(this, EventArgs.Empty);
                }
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
            await PlaybackDiagnosticsLog.ClearAsync();
            PlaybackDiagnosticsLog.WriteBuildMarker(typeof(PlaybackPage));
            PlaybackDiagnosticsLog.WriteLine("Playback page start requested launch=" + (_launchRequest != null));
            if (_launchRequest != null)
            {
                await StartLaunchRequestPlaybackAsync(_launchRequest);
                return;
            }

            await StartManualPlaybackAsync();
        }

        private async Task StartLaunchRequestPlaybackAsync(PlaybackLaunchRequest request)
        {
            if (request.HasDirectStreamUrl)
            {
                await StartDirectStreamQualityRunPlaybackAsync(request);
                return;
            }

            await StartItemPlaybackAsync(request);
        }

        private async Task StartManualPlaybackAsync()
        {
            ManualDebugPanel.Visibility = Visibility.Visible;
            ShowOverlay();
            await EnsureNativeSurfaceReadyAsync();
            var source = CreateManualSource();
            await _orchestrator.StartAsync(DemoItemId, new[] { source }, 0);
            _lastPositionTicks = 0;
            _durationTicks = 0;
            _hasPlaybackContext = _orchestrator.CurrentDescriptor != null;
            _lastPlaybackSessionRequest = null;
            _playbackStoppedReported = false;
            _progressTimer.Stop();
            _currentItemName = "";
            NowPlayingBlock.Text = "Manual Direct Stream";
            UpdateStatus(_orchestrator.State);
            UpdateProgressSlider();
            UpdateControlStates();
            UpdateStreamControls();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task StartItemPlaybackAsync(PlaybackLaunchRequest request)
        {
#if DEBUG
            var qualityStartup = CreateQualityRunStartup(request);
#endif
            await PlaybackDiagnosticsLog.WriteLineAsync(
                "Item playback begin item=" + request.ItemId +
                " requestedSource=" + request.MediaSourceId +
                " startTicks=" + request.StartPositionTicks +
                " runtimeTicks=" + request.RuntimeTicks +
                " forceSdr=" + request.ForceSdrOutput);
            await LogXboxVideoCapabilitiesAsync();
            await PlaybackDiagnosticsLog.WriteLineAsync("Ensure Emby client begin");
            await EnsureEmbyClientAsync();
            await PlaybackDiagnosticsLog.WriteLineAsync("Ensure Emby client end session=" + (_session != null) + " client=" + (_embyClient != null));
            if (_embyClient == null || _session == null)
            {
                throw new InvalidOperationException("Sign in before playback.");
            }

            _currentItemName = request.ItemName;
            _durationTicks = request.RuntimeTicks;
            NowPlayingBlock.Text = string.IsNullOrWhiteSpace(_currentItemName)
                ? request.ItemId
                : _currentItemName;
            UpdateStatus(CorePlaybackState.Opening, "Loading media sources");
            UpdateProgressSlider();
            UpdateControlStates();
            ShowOverlay();
            await Task.Yield();

            var playbackInfoItemId = request.ItemId;
            var playbackInfoClient = _embyClient;
            var playbackInfoSession = _session;
#if DEBUG
            var playbackInfoStartedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "app.prepare",
                request.QualityCommandReceivedAtUtc,
                playbackInfoStartedAtUtc);
#endif
            await PlaybackDiagnosticsLog.WriteLineAsync("PlaybackInfo begin item=" + playbackInfoItemId);
            await PlaybackDiagnosticsLog.WriteLineAsync(
                "PlaybackInfo timeout guard begin timeoutMs=" +
                EmbyRequestTimeoutPolicy.InteractiveRequestTimeout.TotalMilliseconds);
            var sources = await InteractiveRequestGuard.WithTimeoutAsync(
                async () =>
                {
                    await PlaybackDiagnosticsLog.WriteLineAsync(
                        "PlaybackInfo request factory begin item=" + playbackInfoItemId).ConfigureAwait(false);
                    var playbackInfoSources = await playbackInfoClient.GetPlaybackInfoAsync(
                        playbackInfoSession,
                        playbackInfoItemId,
                        request.MediaSourceId).ConfigureAwait(false);
                    await PlaybackDiagnosticsLog.WriteLineAsync(
                        "PlaybackInfo request factory end count=" + playbackInfoSources.Count).ConfigureAwait(false);
                    return playbackInfoSources;
                },
                EmbyRequestTimeoutPolicy.InteractiveRequestTimeout);
#if DEBUG
            var playbackInfoCompletedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "emby.playback-info",
                playbackInfoStartedAtUtc,
                playbackInfoCompletedAtUtc);
#endif
            await PlaybackDiagnosticsLog.WriteLineAsync("PlaybackInfo source count=" + sources.Count);
            if (sources.Count == 0)
            {
                throw new InvalidOperationException("No playable media source was returned by Emby.");
            }

            if (!string.IsNullOrWhiteSpace(request.MediaSourceId))
            {
                var requestedSource = sources.FirstOrDefault(source =>
                    string.Equals(source.Id, request.MediaSourceId, StringComparison.Ordinal));
                if (requestedSource != null)
                {
                    await PlaybackDiagnosticsLog.WriteLineAsync(
                        "Requested source found name=" + requestedSource.Name +
                        " hdr=" + requestedSource.HdrProfile.Kind +
                        " strategy=" + requestedSource.HdrProfile.PlaybackStrategy +
                        " direct=" + requestedSource.HdrProfile.IsDirectPlayable);
                    sources = new[] { requestedSource }
                        .Concat(sources.Where(source => !string.Equals(source.Id, request.MediaSourceId, StringComparison.Ordinal)))
                        .ToList();
                }
                else
                {
                    await PlaybackDiagnosticsLog.WriteLineAsync("Requested source not found");
                }
            }

#if DEBUG
            if (request.ForceSdrOutput)
            {
                await PlaybackDiagnosticsLog.WriteLineAsync("Force SDR output for diagnostics");
                sources = ApplyForcedSdrOutputForDiagnostics(sources);
            }
#endif

#if DEBUG
            var nativeSurfaceStartedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "app.source-selection",
                playbackInfoCompletedAtUtc,
                nativeSurfaceStartedAtUtc);
#endif
            await PlaybackDiagnosticsLog.WriteLineAsync("Ensure native surface begin");
            await EnsureNativeSurfaceReadyAsync();
#if DEBUG
            var nativeSurfaceCompletedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "app.native-surface",
                nativeSurfaceStartedAtUtc,
                nativeSurfaceCompletedAtUtc);
#endif
            await PlaybackDiagnosticsLog.WriteLineAsync("Ensure native surface end");
            UpdateStatus(CorePlaybackState.Opening, "Opening video");
            UpdateProgressSlider();
            UpdateControlStates();
            ShowOverlay();
            await Task.Yield();

#if DEBUG
            var nativeOpenStartedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "app.open-dispatch",
                nativeSurfaceCompletedAtUtc,
                nativeOpenStartedAtUtc);
#endif
            await _orchestrator.StartAsync(request.ItemId, sources, request.StartPositionTicks, request.MediaSourceId);
#if DEBUG
            var playbackStartedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "native.open",
                nativeOpenStartedAtUtc,
                playbackStartedAtUtc);
            CompleteQualityRunStartup(qualityStartup, playbackStartedAtUtc);
#endif
            await PlaybackDiagnosticsLog.WriteLineAsync(
                "Orchestrator start completed state=" + _orchestrator.State +
                " currentSource=" + (_orchestrator.CurrentMediaSource == null ? "" : _orchestrator.CurrentMediaSource.Id));
            _lastPositionTicks = request.StartPositionTicks;
            _hasPlaybackContext = _orchestrator.CurrentDescriptor != null;
            _playbackStoppedReported = false;
            ShowOverlay();
            _progressTimer.Start();
            await ReportPlaybackStartedAsync();
#if DEBUG
            ScheduleQualityRunCapture(request, qualityStartup);
#endif
            UpdateStatus(_orchestrator.State);
            UpdateProgressSlider();
            UpdateControlStates();
            UpdateStreamControls();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task StartDirectStreamQualityRunPlaybackAsync(PlaybackLaunchRequest request)
        {
#if DEBUG
            var qualityStartup = CreateQualityRunStartup(request);
#endif
            await PlaybackDiagnosticsLog.WriteLineAsync(
                "Direct stream quality-run begin runId=" + request.QualityRunId +
                " urlLength=" + request.DirectStreamUrl.Length +
                " startTicks=" + request.StartPositionTicks);

            _currentItemName = string.IsNullOrWhiteSpace(request.ItemName)
                ? GetLaunchRequestDisplayName(request)
                : request.ItemName;
            _durationTicks = request.RuntimeTicks;
            _lastPlaybackSessionRequest = null;
            NowPlayingBlock.Text = _currentItemName;
            ManualDebugPanel.Visibility = Visibility.Collapsed;
            UpdateStatus(CorePlaybackState.Opening, "Opening direct stream");
            UpdateProgressSlider();
            UpdateControlStates();
            ShowOverlay();
            await Task.Yield();

#if DEBUG
            var nativeSurfaceStartedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "app.prepare",
                request.QualityCommandReceivedAtUtc,
                nativeSurfaceStartedAtUtc);
#endif
            await EnsureNativeSurfaceReadyAsync();
#if DEBUG
            var nativeSurfaceCompletedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "app.native-surface",
                nativeSurfaceStartedAtUtc,
                nativeSurfaceCompletedAtUtc);
#endif
            var source = CreateDirectStreamQualityRunSource(request);
#if DEBUG
            var nativeOpenStartedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "app.open-dispatch",
                nativeSurfaceCompletedAtUtc,
                nativeOpenStartedAtUtc);
#endif
            await _orchestrator.StartAsync(
                GetDirectStreamQualityRunItemId(request),
                new[] { source },
                request.StartPositionTicks,
                source.Id);
#if DEBUG
            var playbackStartedAtUtc = DateTimeOffset.UtcNow;
            AddQualityRunStartupStage(
                qualityStartup,
                "native.open",
                nativeOpenStartedAtUtc,
                playbackStartedAtUtc);
            CompleteQualityRunStartup(qualityStartup, playbackStartedAtUtc);
#endif
            await PlaybackDiagnosticsLog.WriteLineAsync(
                "Direct stream quality-run start completed state=" + _orchestrator.State +
                " currentSource=" + (_orchestrator.CurrentMediaSource == null ? "" : _orchestrator.CurrentMediaSource.Id));

            _lastPositionTicks = request.StartPositionTicks;
            _hasPlaybackContext = _orchestrator.CurrentDescriptor != null;
            _playbackStoppedReported = false;
            ShowOverlay();
            _progressTimer.Start();
            await ReportPlaybackStartedAsync();
#if DEBUG
            ScheduleQualityRunCapture(request, qualityStartup);
#endif
            UpdateStatus(_orchestrator.State);
            UpdateProgressSlider();
            UpdateControlStates();
            UpdateStreamControls();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

#if DEBUG
        private static PlaybackQualityStartup CreateQualityRunStartup(PlaybackLaunchRequest request)
        {
            return new PlaybackQualityStartup
            {
                CommandReceivedAt = request.QualityCommandReceivedAtUtc.ToString("O")
            };
        }

        private static void AddQualityRunStartupStage(
            PlaybackQualityStartup startup,
            string name,
            DateTimeOffset startedAtUtc,
            DateTimeOffset completedAtUtc)
        {
            startup.Stages.Add(new PlaybackQualityStartupStage
            {
                Name = name,
                StartedAt = startedAtUtc.ToString("O"),
                CompletedAt = completedAtUtc.ToString("O"),
                DurationMs = Math.Max(0, (completedAtUtc - startedAtUtc).TotalMilliseconds)
            });
        }

        private static void CompleteQualityRunStartup(
            PlaybackQualityStartup startup,
            DateTimeOffset playbackStartedAtUtc)
        {
            startup.PlaybackStartedAt = playbackStartedAtUtc.ToString("O");
            if (DateTimeOffset.TryParse(startup.CommandReceivedAt, out var commandReceivedAtUtc))
            {
                startup.StartupDurationMs =
                    Math.Max(0, (playbackStartedAtUtc - commandReceivedAtUtc).TotalMilliseconds);
            }
        }

        private void ScheduleQualityRunCapture(
            PlaybackLaunchRequest request,
            PlaybackQualityStartup startup)
        {
            if (!request.IsQualityRun)
            {
                return;
            }

            var descriptor = _orchestrator.CurrentDescriptor;
            if (descriptor == null)
            {
                _ = WriteQualityRunCommandResultAsync(
                    "capture-skipped",
                    "quality-run has no current playback descriptor");
                return;
            }

            _ = CaptureQualityRunAsync(request, descriptor, startup);
        }

        private async Task CaptureQualityRunAsync(
            PlaybackLaunchRequest request,
            PlaybackDescriptor descriptor,
            PlaybackQualityStartup startup)
        {
            try
            {
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "QualityRun capture scheduled runId=" + request.QualityRunId +
                    " durationSeconds=" + request.QualityRunDurationSeconds +
                    " startupStages=" + startup.Stages.Count);

                var referenceCase = PlaybackQualityCaptureReferenceCaseFactory.Create(
                    request.QualityRunId,
                    descriptor,
                    request.QualityExpected,
                    request.QualityScenario,
                    uri: request.QualitySourceLocator);
                var lifecycle = CreateQualityRunLifecycle(
                    request.StartPositionTicks,
                    GetCurrentPositionTicks(),
                    _orchestrator.State.ToString());
                var position = CreateQualityRunPosition(request.StartPositionTicks);
                var interaction = await RunQualityRunScenarioAsync(
                    request,
                    descriptor,
                    lifecycle,
                    position,
                    DateTimeOffset.UtcNow);
                var capturedDescriptor = _orchestrator.CurrentDescriptor ?? descriptor;
                var evidence = CaptureQualityRunEvidence(_backend, capturedDescriptor);
                EnrichQualityRunTimelineEvidence(position, evidence.MetricsProvider);
                await StopQualityRunPlaybackAsync(lifecycle);

                var environment = new PlaybackQualityEnvironment
                {
                    CollectorVersion = "app-hosted-quality-run-v0.1",
                    PlayerCoreVersion = "NoiraPlayer.Core",
                    SourceRevision = request.QualitySourceRevision,
                    BuildConfiguration = "Debug"
                };
                var execution = CreateAppHostedExecutionEvidence(
                    referenceCase,
                    request.QualityCommandReceivedAtUtc,
                    PlaybackQualityExecutionStatus.Completed,
                    sourceOpened: true,
                    evidence.MetricsProvider);

                var result = PlaybackQualityRuntimeEvidenceCollector.ComposeRunResult(
                    referenceCase,
                    capturedDescriptor,
                    evidence.Diagnostics,
                    evidence.MetricsProvider,
                    startup,
                    environment,
                    lifecycle,
                    position,
                    execution,
                    interaction);
                if (result.Report.Execution.SourceOpened)
                {
                    result.Report.Execution.OpenedSourceHash =
                        PlaybackQualitySourceFingerprint.ComputeOpenedMediaSignature(result.Report);
                    result.Report.Execution.OpenedSourceHashKind =
                        PlaybackQualitySourceFingerprint.OpenedMediaSignatureKind;
                }
                var relativePath = await WriteQualityRunReportAsync(request.QualityRunId, result);
                await WriteQualityRunCommandResultAsync("captured", relativePath);
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "QualityRun capture wrote " + relativePath);
            }
            catch (Exception ex)
            {
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "QualityRun capture exception " + ex.GetType().FullName + " " + ex.Message);
                await WriteQualityRunCommandResultAsync(
                    "capture-exception",
                    ex.GetType().FullName + " " + ex.Message);
            }
        }

        private static PlaybackQualityLifecycle CreateQualityRunLifecycle(
            long startPositionTicks,
            long capturedPositionTicks,
            string state)
        {
            var lifecycle = new PlaybackQualityLifecycle();
            lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
            {
                Operation = "load",
                Status = "success",
                State = state,
                PositionTicks = startPositionTicks,
                Message = "app-hosted quality-run opened playback"
            });
            lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
            {
                Operation = "play",
                Status = "success",
                State = state,
                PositionTicks = capturedPositionTicks,
                Message = "app-hosted quality-run captured playback sample"
            });

            return lifecycle;
        }

        private static PlaybackQualityPosition CreateQualityRunPosition(long requestedStartPositionTicks)
        {
            return new PlaybackQualityPosition
            {
                RequestedStartPositionTicks = Math.Max(0, requestedStartPositionTicks)
            };
        }

        private async Task<PlaybackQualityInteractionEvidence?> RunQualityRunScenarioAsync(
            PlaybackLaunchRequest request,
            PlaybackDescriptor descriptor,
            PlaybackQualityLifecycle lifecycle,
            PlaybackQualityPosition position,
            DateTimeOffset probeStartedAtUtc)
        {
            var duration = TimeSpan.FromSeconds(request.QualityRunDurationSeconds);
            var firstDelay = GetQualityRunProbeDelay(duration, 0.20);
            var shortDelay = GetQualityRunProbeDelay(duration, 0.05);
            PlaybackQualityInteractionEvidence? interaction = null;

            switch (request.QualityScenario)
            {
                case PlaybackQualityExecutionScenario.PauseResume:
                    await Task.Delay(firstDelay);
                    await _orchestrator.PauseAsync();
                    var positionBeforePauseTicks = GetCurrentPositionTicks();
                    TryReadQualityRunInteractionMetrics(
                        out var renderedVideoFramesBefore,
                        out _);
                    var pauseDuration = request.QualityPauseSeconds > 0
                        ? TimeSpan.FromSeconds(request.QualityPauseSeconds)
                        : shortDelay;
                    await Task.Delay(pauseDuration);
                    var positionDuringPauseTicks = GetCurrentPositionTicks();
                    var positionBeforeResumeTicks = positionDuringPauseTicks;
                    await _orchestrator.ResumeAsync();
                    var pauseResumeTimeoutAt = DateTimeOffset.UtcNow.AddSeconds(5);
                    var positionAfterResumeTicks = GetCurrentPositionTicks();
                    var renderedVideoFramesAfter = renderedVideoFramesBefore;
                    var pauseResumeRecovered = false;
                    while (DateTimeOffset.UtcNow < pauseResumeTimeoutAt)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                        positionAfterResumeTicks = GetCurrentPositionTicks();
                        TryReadQualityRunInteractionMetrics(
                            out renderedVideoFramesAfter,
                            out _);
                        pauseResumeRecovered =
                            PlaybackQualityInteractionEvidencePolicy.IsPauseResumeRecovered(
                                positionBeforePauseTicks,
                                positionDuringPauseTicks,
                                positionBeforeResumeTicks,
                                positionAfterResumeTicks,
                                renderedVideoFramesBefore,
                                renderedVideoFramesAfter);
                        if (pauseResumeRecovered)
                        {
                            break;
                        }
                    }
                    AddQualityRunLifecycleEvent(
                        lifecycle,
                        "pause",
                        pauseResumeRecovered ? "success" : "failed",
                        "app-hosted pause-resume paused position=" +
                        positionBeforePauseTicks + "->" + positionDuringPauseTicks +
                        " requestedPauseSeconds=" + request.QualityPauseSeconds);
                    AddQualityRunLifecycleEvent(
                        lifecycle,
                        "resume",
                        pauseResumeRecovered ? "success" : "failed",
                        "app-hosted pause-resume resumed position=" +
                        positionBeforeResumeTicks + "->" + positionAfterResumeTicks +
                        " renderedVideoFrames=" + renderedVideoFramesBefore +
                        "->" + renderedVideoFramesAfter);
                    break;

                case PlaybackQualityExecutionScenario.Timeline:
                    await Task.Delay(firstDelay);
                    await RunQualityRunSeekProbeAsync(position, lifecycle);
                    break;

                case PlaybackQualityExecutionScenario.AudioSwitch:
                    await Task.Delay(firstDelay);
                    interaction = await RunQualityRunAudioSwitchScenarioAsync(descriptor, lifecycle);
                    break;

                case PlaybackQualityExecutionScenario.SubtitleSwitch:
                    await Task.Delay(firstDelay);
                    interaction = await RunQualityRunSubtitleSwitchScenarioAsync(descriptor, lifecycle);
                    break;

                case PlaybackQualityExecutionScenario.Playback:
                    break;

                default:
                    throw new InvalidOperationException(
                        "Unknown playback quality scenario: " + request.QualityScenario);
            }

            var elapsed = DateTimeOffset.UtcNow - probeStartedAtUtc;
            if (elapsed < duration)
            {
                await Task.Delay(duration - elapsed);
            }

            return interaction;
        }

        private async Task<PlaybackQualityInteractionEvidence?> RunQualityRunAudioSwitchScenarioAsync(
            PlaybackDescriptor descriptor,
            PlaybackQualityLifecycle lifecycle)
        {
            var positionBeforeTicks = GetCurrentPositionTicks();
            var hasBeforeMetrics = TryReadQualityRunMetrics(out var beforeMetrics);
            beforeMetrics.VideoPositionTicks = positionBeforeTicks;
            var selectedBefore = beforeMetrics.SelectedAudioStreamIndex >= 0
                ? beforeMetrics.SelectedAudioStreamIndex
                : descriptor.AudioStreamIndex.GetValueOrDefault(-1);
            var target = descriptor.MediaSource.AudioStreams.FirstOrDefault(
                stream => stream.Index != selectedBefore);
            if (target == null)
            {
                AddQualityRunLifecycleEvent(
                    lifecycle,
                    "audio-switch",
                    "failed",
                    "app-hosted audio-switch scenario has no alternative audio stream");
                return null;
            }

            var submittedAudioFramesBefore = beforeMetrics.SubmittedAudioFrames;
            var stopwatch = Stopwatch.StartNew();
            await _orchestrator.SwitchAudioStreamAsync(target.Index);
            var operationDurationMs = stopwatch.Elapsed.TotalMilliseconds;
            var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(8);
            var selected = _orchestrator.CurrentDescriptor?.AudioStreamIndex ?? -1;
            var positionAfterTicks = GetCurrentPositionTicks();
            var submittedAudioFramesAfter = submittedAudioFramesBefore;
            var recovered = false;
            var hasAfterMetrics = false;
            var afterMetrics = new PlaybackQualityMetricsSnapshot();
            while (DateTimeOffset.UtcNow < timeoutAt)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                selected = _orchestrator.CurrentDescriptor?.AudioStreamIndex ?? -1;
                positionAfterTicks = GetCurrentPositionTicks();
                hasAfterMetrics = TryReadQualityRunMetrics(out afterMetrics);
                submittedAudioFramesAfter = afterMetrics.SubmittedAudioFrames;
                recovered = PlaybackQualityInteractionEvidencePolicy.IsAudioSwitchRecovered(
                    target.Index,
                    selected,
                    positionBeforeTicks,
                    positionAfterTicks,
                    submittedAudioFramesBefore,
                    submittedAudioFramesAfter);
                if (recovered)
                {
                    break;
                }
            }
            var recoveryDurationMs = stopwatch.Elapsed.TotalMilliseconds;
            afterMetrics.VideoPositionTicks = positionAfterTicks;
            AddQualityRunLifecycleEvent(
                lifecycle,
                "audio-switch",
                recovered ? "success" : "failed",
                "app-hosted audio-switch target=" + target.Index +
                " selected=" + selected +
                " position=" + positionBeforeTicks + "->" + positionAfterTicks +
                " submittedAudioFrames=" + submittedAudioFramesBefore +
                "->" + submittedAudioFramesAfter);

            return hasBeforeMetrics && hasAfterMetrics
                ? CreateQualityRunInteractionEvidence(
                    "audio-switch",
                    operationDurationMs,
                    recoveryDurationMs,
                    null,
                    beforeMetrics,
                    afterMetrics,
                    lifecycle)
                : null;
        }

        private bool TryReadQualityRunInteractionMetrics(
            out ulong renderedVideoFrames,
            out ulong submittedAudioFrames)
        {
            renderedVideoFrames = 0;
            submittedAudioFrames = 0;
            if (!(_backend is IPlaybackQualityMetricsProvider provider) ||
                !provider.TryGetQualityMetrics(out var metrics) ||
                metrics == null)
            {
                return false;
            }

            renderedVideoFrames = metrics.RenderedVideoFrames;
            submittedAudioFrames = metrics.SubmittedAudioFrames;
            return true;
        }

        private bool TryReadQualityRunMetrics(out PlaybackQualityMetricsSnapshot metrics)
        {
            metrics = new PlaybackQualityMetricsSnapshot();
            return _backend is IPlaybackQualityMetricsProvider provider &&
                provider.TryGetQualityMetrics(out metrics) &&
                metrics != null;
        }

        private async Task<PlaybackQualityInteractionEvidence?> RunQualityRunSubtitleSwitchScenarioAsync(
            PlaybackDescriptor descriptor,
            PlaybackQualityLifecycle lifecycle)
        {
            var target = descriptor.MediaSource.SubtitleStreams.FirstOrDefault(
                stream => stream.Index != descriptor.SubtitleStreamIndex);
            if (target == null)
            {
                AddQualityRunLifecycleEvent(
                    lifecycle,
                    "subtitle-switch",
                    "failed",
                    "app-hosted subtitle-switch scenario has no selectable subtitle stream");
                return null;
            }

            var baselineCueCount = ReadQualityRunSubtitleCueCount();
            var baselineDecodedCueCount = ReadQualityRunSubtitleDecodedCueCount();
            await _orchestrator.PauseAsync();
            AddQualityRunLifecycleEvent(
                lifecycle,
                "pause",
                "success",
                "app-hosted subtitle-switch scenario paused playback");
            var positionBeforeTicks = GetCurrentPositionTicks();
            var hasBeforeMetrics = TryReadQualityRunMetrics(out var beforeMetrics);
            beforeMetrics.VideoPositionTicks = positionBeforeTicks;
            var stopwatch = Stopwatch.StartNew();
            await _orchestrator.SwitchSubtitleStreamAsync(target.Index);
            var operationDurationMs = stopwatch.Elapsed.TotalMilliseconds;
            await _orchestrator.ResumeAsync();
            AddQualityRunLifecycleEvent(
                lifecycle,
                "resume",
                "success",
                "app-hosted subtitle-switch scenario resumed playback");

            var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(8);
            var selectedIndex = -1;
            var decodedCueCount = baselineDecodedCueCount;
            var cueCount = baselineCueCount;
            var positionAfterTicks = positionBeforeTicks;
            var renderedVideoFramesAfter = beforeMetrics.RenderedVideoFrames;
            var playbackRecovered = false;
            double? recoveryDurationMs = null;
            double? cueRenderDurationMs = null;
            var hasAfterMetrics = false;
            var afterMetrics = new PlaybackQualityMetricsSnapshot();
            while (DateTimeOffset.UtcNow < timeoutAt)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                var hasSubtitleState = TryReadQualityRunSubtitleState(
                        out selectedIndex,
                        out decodedCueCount,
                        out cueCount);
                hasAfterMetrics = TryReadQualityRunMetrics(out afterMetrics);
                positionAfterTicks = GetCurrentPositionTicks();
                renderedVideoFramesAfter = afterMetrics.RenderedVideoFrames;
                if (!playbackRecovered &&
                    selectedIndex == target.Index &&
                    positionAfterTicks > positionBeforeTicks &&
                    renderedVideoFramesAfter > beforeMetrics.RenderedVideoFrames)
                {
                    playbackRecovered = true;
                    recoveryDurationMs = stopwatch.Elapsed.TotalMilliseconds;
                }
                if (!cueRenderDurationMs.HasValue &&
                    hasSubtitleState &&
                    selectedIndex == target.Index &&
                    cueCount > baselineCueCount)
                {
                    cueRenderDurationMs = stopwatch.Elapsed.TotalMilliseconds;
                }
                if (playbackRecovered && cueRenderDurationMs.HasValue)
                {
                    break;
                }
            }

            var succeeded = playbackRecovered && cueRenderDurationMs.HasValue;
            afterMetrics.VideoPositionTicks = positionAfterTicks;
            AddQualityRunLifecycleEvent(
                lifecycle,
                "subtitle-switch",
                succeeded ? "success" : "failed",
                "app-hosted subtitle-switch target=" + target.Index +
                " selected=" + selectedIndex +
                " position=" + positionBeforeTicks + "->" + positionAfterTicks +
                " renderedVideoFrames=" + beforeMetrics.RenderedVideoFrames + "->" + renderedVideoFramesAfter +
                " decodedCueCount=" + baselineDecodedCueCount + "->" + decodedCueCount +
                " cueRenderCount=" + baselineCueCount + "->" + cueCount);

            return hasBeforeMetrics && hasAfterMetrics
                ? CreateQualityRunInteractionEvidence(
                    "subtitle-switch",
                    operationDurationMs,
                    recoveryDurationMs ?? stopwatch.Elapsed.TotalMilliseconds,
                    cueRenderDurationMs,
                    beforeMetrics,
                    afterMetrics,
                    lifecycle)
                : null;
        }

        private static PlaybackQualityInteractionEvidence? CreateQualityRunInteractionEvidence(
            string scenario,
            double operationDurationMs,
            double recoveryDurationMs,
            double? cueRenderDurationMs,
            PlaybackQualityMetricsSnapshot before,
            PlaybackQualityMetricsSnapshot after,
            PlaybackQualityLifecycle lifecycle)
        {
            try
            {
                return PlaybackQualityInteractionCapture.Create(
                    scenario,
                    operationDurationMs,
                    recoveryDurationMs,
                    cueRenderDurationMs,
                    before,
                    after);
            }
            catch (InvalidOperationException ex)
            {
                lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
                {
                    Operation = scenario,
                    Status = "failed",
                    Message = "app-hosted interaction evidence rejected: " + ex.Message
                });
                return null;
            }
        }

        private ulong ReadQualityRunSubtitleCueCount()
        {
            return TryReadQualityRunSubtitleState(out _, out _, out var cueCount)
                ? cueCount
                : 0;
        }

        private ulong ReadQualityRunSubtitleDecodedCueCount()
        {
            return TryReadQualityRunSubtitleState(out _, out var decodedCueCount, out _)
                ? decodedCueCount
                : 0;
        }

        private bool TryReadQualityRunSubtitleState(
            out int selectedSubtitleStreamIndex,
            out ulong subtitleDecodedCueCount,
            out ulong subtitleCueRenderCount)
        {
            selectedSubtitleStreamIndex = -1;
            subtitleDecodedCueCount = 0;
            subtitleCueRenderCount = 0;
            if (!(_backend is IPlaybackQualityMetricsProvider provider) ||
                !provider.TryGetQualityMetrics(out var metrics) ||
                metrics == null)
            {
                return false;
            }

            selectedSubtitleStreamIndex = metrics.SelectedSubtitleStreamIndex;
            subtitleDecodedCueCount = metrics.SubtitleDecodedCueCount;
            subtitleCueRenderCount = metrics.SubtitleCueRenderCount;
            return true;
        }

        private async Task RunQualityRunSeekProbeAsync(
            PlaybackQualityPosition position,
            PlaybackQualityLifecycle lifecycle)
        {
            var shortDelay = GetQualityRunProbeDelay(TimeSpan.FromSeconds(1), 0.10);
            if (IsPlaybackSeekable())
            {
                var seekTargetTicks = CalculateQualityRunSeekTargetTicks();
                position.SeekTargetPositionTicks = seekTargetTicks;
                await _orchestrator.SeekAsync(seekTargetTicks);
                await Task.Delay(shortDelay);
                var actualPositionTicks = GetCurrentPositionTicks();
                position.ActualPositionTicks = actualPositionTicks;
                position.SeekPositionErrorMs =
                    Math.Abs(actualPositionTicks - seekTargetTicks) / 10000.0;
                AddQualityRunLifecycleEvent(
                    lifecycle,
                    "seek",
                    "success",
                    "app-hosted quality-run seek completed after runtime evidence capture");
            }
            else
            {
                position.ActualPositionTicks = GetCurrentPositionTicks();
                AddQualityRunLifecycleEvent(
                    lifecycle,
                    "seek",
                    "skipped",
                    "app-hosted quality-run skipped seek because playback was not seekable");
            }
        }

        private async Task StopQualityRunPlaybackAsync(PlaybackQualityLifecycle lifecycle)
        {
            await _orchestrator.StopAsync();
            AddQualityRunLifecycleEvent(
                lifecycle,
                "stop",
                "success",
                "app-hosted quality-run stopped playback");
        }

        private static void EnrichQualityRunTimelineEvidence(
            PlaybackQualityPosition position,
            IPlaybackQualityMetricsProvider? metricsProvider)
        {
            if (!position.SeekTargetPositionTicks.HasValue ||
                metricsProvider == null ||
                !metricsProvider.TryGetQualityMetrics(out var metrics) ||
                metrics == null)
            {
                return;
            }

            position.SeekDemuxTargetTicks = metrics.SeekDemuxTargetTicks;
            position.FirstPresentedPositionTicks = metrics.FirstPresentedPositionTicks;
            if (metrics.FirstPresentedPositionTicks.HasValue)
            {
                position.ActualPositionTicks = metrics.FirstPresentedPositionTicks;
                position.SeekPositionErrorMs = Math.Abs(
                    metrics.FirstPresentedPositionTicks.Value - position.SeekTargetPositionTicks.Value) / 10000.0;
            }

            position.PostSeekPositionTicks = metrics.VideoPositionTicks;
            position.PostSeekAdvanced =
                metrics.FirstPresentedPositionTicks.HasValue &&
                metrics.VideoPositionTicks > metrics.FirstPresentedPositionTicks.Value;
        }

        private static TimeSpan GetQualityRunProbeDelay(TimeSpan duration, double ratio)
        {
            var milliseconds = Math.Max(250, Math.Min(1500, duration.TotalMilliseconds * ratio));
            return TimeSpan.FromMilliseconds(milliseconds);
        }

        private long CalculateQualityRunSeekTargetTicks()
        {
            var currentPositionTicks = GetCurrentPositionTicks();
            var targetTicks = currentPositionTicks + TimeSpan.FromSeconds(1).Ticks;
            if (_durationTicks > TimeSpan.FromSeconds(2).Ticks)
            {
                targetTicks = Math.Min(targetTicks, _durationTicks - TimeSpan.FromSeconds(1).Ticks);
            }

            return ClampToDuration(Math.Max(0, targetTicks));
        }

        private void AddQualityRunLifecycleEvent(
            PlaybackQualityLifecycle lifecycle,
            string operation,
            string status,
            string message)
        {
            lifecycle.Events.Add(new PlaybackQualityLifecycleEvent
            {
                Operation = operation,
                Status = status,
                State = _orchestrator.State.ToString(),
                PositionTicks = GetCurrentPositionTicks(),
                Message = message
            });
        }

        private static QualityRunEvidence CaptureQualityRunEvidence(
            IPlaybackBackend backend,
            PlaybackDescriptor descriptor)
        {
            var diagnostics = backend as IPlaybackBackendDiagnostics;
            var capturedDiagnostics = diagnostics == null
                ? null
                : new CapturedPlaybackBackendDiagnostics(
                    diagnostics.Capabilities,
                    CreateQualityRunDisplayStatus(diagnostics.DisplayStatus, descriptor));
            var metricsProvider = backend as IPlaybackQualityMetricsProvider;
            var capturedMetricsProvider = metricsProvider == null
                ? null
                : CapturedPlaybackQualityMetricsProvider.Capture(metricsProvider);

            return new QualityRunEvidence(capturedDiagnostics, capturedMetricsProvider);
        }

        private static PlaybackDisplayStatus CreateQualityRunDisplayStatus(
            PlaybackDisplayStatus status,
            PlaybackDescriptor descriptor)
        {
            var refreshRateHz = status.RefreshRateHz;
            if (refreshRateHz <= 0.0)
            {
                refreshRateHz = PlaybackRefreshRatePolicy.SelectSoftwareOnlyRefreshRateSnapshot(
                    descriptor.MediaSource.VideoFrameRate);
            }

            if (refreshRateHz <= 0.0)
            {
                return status;
            }

            var message = status.Message ?? "";
            if (status.RefreshRateHz <= 0.0)
            {
                message = string.IsNullOrWhiteSpace(message)
                    ? "Display refresh rate uses software-only cadence estimate."
                    : message + " Display refresh rate uses software-only cadence estimate.";
            }

            return new PlaybackDisplayStatus(
                status.HdrStatus,
                status.IsHdrDisplayAvailable,
                status.IsHdrOutputActive,
                message,
                status.SwapChainFormat,
                status.SwapChainColorSpace,
                status.IsTenBitSwapChain,
                status.IsVideoProcessorColorSpaceValidated,
                status.VideoProcessorInputColorSpace,
                status.VideoProcessorOutputColorSpace,
                status.VideoProcessorConversionStatus,
                refreshRateHz);
        }

        private sealed class QualityRunEvidence
        {
            public QualityRunEvidence(
                IPlaybackBackendDiagnostics? diagnostics,
                IPlaybackQualityMetricsProvider? metricsProvider)
            {
                Diagnostics = diagnostics;
                MetricsProvider = metricsProvider;
            }

            public IPlaybackBackendDiagnostics? Diagnostics { get; }

            public IPlaybackQualityMetricsProvider? MetricsProvider { get; }
        }

        private sealed class CapturedPlaybackBackendDiagnostics : IPlaybackBackendDiagnostics
        {
            public CapturedPlaybackBackendDiagnostics(
                PlaybackBackendCapabilities capabilities,
                PlaybackDisplayStatus displayStatus)
            {
                Capabilities = capabilities;
                DisplayStatus = displayStatus;
            }

            public PlaybackBackendCapabilities Capabilities { get; }

            public PlaybackDisplayStatus DisplayStatus { get; }
        }

        private sealed class CapturedPlaybackQualityMetricsProvider :
            IPlaybackQualityMetricsProvider,
            IPlaybackQualityMetricsProviderIdentity
        {
            private readonly bool _hasMetrics;
            private readonly PlaybackQualityMetricsSnapshot _metrics;

            private CapturedPlaybackQualityMetricsProvider(
                string providerId,
                bool hasMetrics,
                PlaybackQualityMetricsSnapshot metrics)
            {
                PlaybackQualityMetricsProviderId = providerId;
                _hasMetrics = hasMetrics;
                _metrics = metrics;
            }

            public string PlaybackQualityMetricsProviderId { get; }

            public static CapturedPlaybackQualityMetricsProvider Capture(
                IPlaybackQualityMetricsProvider provider)
            {
                var providerId = "captured";
                if (provider is IPlaybackQualityMetricsProviderIdentity identity &&
                    !string.IsNullOrWhiteSpace(identity.PlaybackQualityMetricsProviderId))
                {
                    providerId = identity.PlaybackQualityMetricsProviderId;
                }

                var hasMetrics = provider.TryGetQualityMetrics(out var metrics);
                if (metrics == null)
                {
                    metrics = new PlaybackQualityMetricsSnapshot();
                }

                return new CapturedPlaybackQualityMetricsProvider(
                    providerId,
                    hasMetrics,
                    Clone(metrics));
            }

            public bool TryGetQualityMetrics(out PlaybackQualityMetricsSnapshot metrics)
            {
                metrics = Clone(_metrics);
                return _hasMetrics;
            }

            private static PlaybackQualityMetricsSnapshot Clone(PlaybackQualityMetricsSnapshot source)
            {
                return new PlaybackQualityMetricsSnapshot
                {
                    RenderPasses = source.RenderPasses,
                    DecodedVideoFrames = source.DecodedVideoFrames,
                    HardwareDecodedVideoFrames = source.HardwareDecodedVideoFrames,
                    SoftwareDecodedVideoFrames = source.SoftwareDecodedVideoFrames,
                    RenderedVideoFrames = source.RenderedVideoFrames,
                    SubmittedAudioFrames = source.SubmittedAudioFrames,
                    SelectedAudioStreamIndex = source.SelectedAudioStreamIndex,
                    SubtitleDecodedCueCount = source.SubtitleDecodedCueCount,
                    SubtitleCueRenderCount = source.SubtitleCueRenderCount,
                    SelectedSubtitleStreamIndex = source.SelectedSubtitleStreamIndex,
                    DroppedVideoFrames = source.DroppedVideoFrames,
                    SeekPrerollDroppedFrames = source.SeekPrerollDroppedFrames,
                    VideoAheadWaitCount = source.VideoAheadWaitCount,
                    AudioAheadWaitCount = source.AudioAheadWaitCount,
                    VideoClockWaitCount = source.VideoClockWaitCount,
                    VideoStarvedPasses = source.VideoStarvedPasses,
                    AudioStarvedPasses = source.AudioStarvedPasses,
                    QueuedAudioBuffers = source.QueuedAudioBuffers,
                    AudioClockTicks = source.AudioClockTicks,
                    VideoPositionTicks = source.VideoPositionTicks,
                    NativeGraphOpenDurationMs = source.NativeGraphOpenDurationMs,
                    FfmpegOpenInputDurationMs = source.FfmpegOpenInputDurationMs,
                    FfmpegStreamInfoDurationMs = source.FfmpegStreamInfoDurationMs,
                    NativeStartupSeekDurationMs = source.NativeStartupSeekDurationMs,
                    NativeFirstFrameDurationMs = source.NativeFirstFrameDurationMs,
                    NativeFirstFrameDemuxReadDurationMs = source.NativeFirstFrameDemuxReadDurationMs,
                    NativeFirstFramePresentDurationMs = source.NativeFirstFramePresentDurationMs,
                    NativeFirstFrameDemuxPacketCount = source.NativeFirstFrameDemuxPacketCount,
                    NativeFirstFrameDemuxBytes = source.NativeFirstFrameDemuxBytes,
                    ContainerStartTimeTicks = source.ContainerStartTimeTicks,
                    VideoStreamStartTimeTicks = source.VideoStreamStartTimeTicks,
                    SeekDemuxTargetTicks = source.SeekDemuxTargetTicks,
                    FirstPresentedPositionTicks = source.FirstPresentedPositionTicks,
                    RenderIntervalMsP05 = source.RenderIntervalMsP05,
                    RenderIntervalMsP50 = source.RenderIntervalMsP50,
                    RenderIntervalMsP95 = source.RenderIntervalMsP95,
                    RenderIntervalMsP99 = source.RenderIntervalMsP99,
                    MinFrameGapMs = source.MinFrameGapMs,
                    MaxFrameGapMs = source.MaxFrameGapMs,
                    RenderIntervalSampleCount = source.RenderIntervalSampleCount,
                    RenderIntervalOverExpected2MsCount = source.RenderIntervalOverExpected2MsCount,
                    RenderIntervalOverExpected4MsCount = source.RenderIntervalOverExpected4MsCount,
                    RenderIntervalUnderExpected2MsCount = source.RenderIntervalUnderExpected2MsCount,
                    RenderIntervalUnderExpected4MsCount = source.RenderIntervalUnderExpected4MsCount,
                    RenderIntervalAfterAudioAheadWaitSampleCount = source.RenderIntervalAfterAudioAheadWaitSampleCount,
                    RenderIntervalAfterAudioAheadWaitMsP95 = source.RenderIntervalAfterAudioAheadWaitMsP95,
                    RenderIntervalAfterAudioAheadWaitMsP99 = source.RenderIntervalAfterAudioAheadWaitMsP99,
                    RenderIntervalAfterAudioAheadWaitMsMax = source.RenderIntervalAfterAudioAheadWaitMsMax,
                    AudioAheadWaitEndToPresentSampleCount = source.AudioAheadWaitEndToPresentSampleCount,
                    AudioAheadWaitEndToPresentMsP50 = source.AudioAheadWaitEndToPresentMsP50,
                    AudioAheadWaitEndToPresentMsP95 = source.AudioAheadWaitEndToPresentMsP95,
                    AudioAheadWaitEndToPresentMsP99 = source.AudioAheadWaitEndToPresentMsP99,
                    AudioAheadWaitEndToPresentMsMax = source.AudioAheadWaitEndToPresentMsMax,
                    RenderIntervalAfterNonAudioWaitSampleCount = source.RenderIntervalAfterNonAudioWaitSampleCount,
                    RenderIntervalAfterNonAudioWaitMsP95 = source.RenderIntervalAfterNonAudioWaitMsP95,
                    RenderIntervalAfterNonAudioWaitMsP99 = source.RenderIntervalAfterNonAudioWaitMsP99,
                    RenderIntervalAfterNonAudioWaitMsMax = source.RenderIntervalAfterNonAudioWaitMsMax,
                    PresentDurationMsP50 = source.PresentDurationMsP50,
                    PresentDurationMsP95 = source.PresentDurationMsP95,
                    PresentDurationMsP99 = source.PresentDurationMsP99,
                    PresentDurationMsMax = source.PresentDurationMsMax,
                    AudioAheadWaitDurationMsP50 = source.AudioAheadWaitDurationMsP50,
                    AudioAheadWaitDurationMsP95 = source.AudioAheadWaitDurationMsP95,
                    AudioAheadWaitDurationMsP99 = source.AudioAheadWaitDurationMsP99,
                    AudioAheadWaitDurationMsMax = source.AudioAheadWaitDurationMsMax,
                    AudioAheadWaitTargetMsP50 = source.AudioAheadWaitTargetMsP50,
                    AudioAheadWaitTargetMsP95 = source.AudioAheadWaitTargetMsP95,
                    AudioAheadWaitTargetMsP99 = source.AudioAheadWaitTargetMsP99,
                    AudioAheadWaitTargetMsMax = source.AudioAheadWaitTargetMsMax,
                    AudioAheadWaitOversleepMsP50 = source.AudioAheadWaitOversleepMsP50,
                    AudioAheadWaitOversleepMsP95 = source.AudioAheadWaitOversleepMsP95,
                    AudioAheadWaitOversleepMsP99 = source.AudioAheadWaitOversleepMsP99,
                    AudioAheadWaitOversleepMsMax = source.AudioAheadWaitOversleepMsMax,
                    AudioAheadWaitFinalDeltaAbsMsP50 = source.AudioAheadWaitFinalDeltaAbsMsP50,
                    AudioAheadWaitFinalDeltaAbsMsP95 = source.AudioAheadWaitFinalDeltaAbsMsP95,
                    AudioAheadWaitFinalDeltaAbsMsP99 = source.AudioAheadWaitFinalDeltaAbsMsP99,
                    AudioAheadWaitFinalDeltaAbsMsMax = source.AudioAheadWaitFinalDeltaAbsMsMax,
                    AudioAheadWaitEpisodeCount = source.AudioAheadWaitEpisodeCount,
                    AudioAheadWaitPassesPerEpisodeP50 = source.AudioAheadWaitPassesPerEpisodeP50,
                    AudioAheadWaitPassesPerEpisodeP95 = source.AudioAheadWaitPassesPerEpisodeP95,
                    AudioAheadWaitPassesPerEpisodeP99 = source.AudioAheadWaitPassesPerEpisodeP99,
                    AudioAheadWaitPassesPerEpisodeMax = source.AudioAheadWaitPassesPerEpisodeMax,
                    AudioAheadWaitPassDurationMsP50 = source.AudioAheadWaitPassDurationMsP50,
                    AudioAheadWaitPassDurationMsP95 = source.AudioAheadWaitPassDurationMsP95,
                    AudioAheadWaitPassDurationMsP99 = source.AudioAheadWaitPassDurationMsP99,
                    AudioAheadWaitPassDurationMsMax = source.AudioAheadWaitPassDurationMsMax,
                    AudioAheadWaitPassTargetMsP50 = source.AudioAheadWaitPassTargetMsP50,
                    AudioAheadWaitPassTargetMsP95 = source.AudioAheadWaitPassTargetMsP95,
                    AudioAheadWaitPassTargetMsP99 = source.AudioAheadWaitPassTargetMsP99,
                    AudioAheadWaitPassTargetMsMax = source.AudioAheadWaitPassTargetMsMax,
                    AudioAheadWaitPassOversleepMsP50 = source.AudioAheadWaitPassOversleepMsP50,
                    AudioAheadWaitPassOversleepMsP95 = source.AudioAheadWaitPassOversleepMsP95,
                    AudioAheadWaitPassOversleepMsP99 = source.AudioAheadWaitPassOversleepMsP99,
                    AudioAheadWaitPassOversleepMsMax = source.AudioAheadWaitPassOversleepMsMax,
                    FramePacingSourceFrameRate = source.FramePacingSourceFrameRate,
                    LateFrameDropToleranceMs = source.LateFrameDropToleranceMs,
                    AudioVideoDriftMsP50 = source.AudioVideoDriftMsP50,
                    AudioVideoDriftMsP95 = source.AudioVideoDriftMsP95,
                    AudioVideoDriftMsP99 = source.AudioVideoDriftMsP99,
                    AudioVideoDriftMsMax = source.AudioVideoDriftMsMax,
                    LastInteractionScenario = source.LastInteractionScenario,
                    LastInteractionSequence = source.LastInteractionSequence,
                    LastInteractionLockWaitDurationMs = source.LastInteractionLockWaitDurationMs,
                    LastInteractionExecutionDurationMs = source.LastInteractionExecutionDurationMs,
                    LastInteractionQuiesceDurationMs = source.LastInteractionQuiesceDurationMs,
                    LastInteractionSeekDurationMs = source.LastInteractionSeekDurationMs,
                    LastInteractionDecoderOpenDurationMs = source.LastInteractionDecoderOpenDurationMs,
                    LastInteractionRendererOpenDurationMs = source.LastInteractionRendererOpenDurationMs,
                    LastInteractionPacketCacheHit = source.LastInteractionPacketCacheHit,
                    LastInteractionPacketCacheEnabled = source.LastInteractionPacketCacheEnabled,
                    LastInteractionPacketCachePacketCount = source.LastInteractionPacketCachePacketCount,
                    LastInteractionPacketCacheBytes = source.LastInteractionPacketCacheBytes,
                    LastInteractionPacketCacheWindowDurationTicks = source.LastInteractionPacketCacheWindowDurationTicks
                };
            }
        }

        private static async Task<string> WriteQualityRunReportAsync(
            string runId,
            PlaybackQualityRunResult result)
        {
            var reportRelativePath = PlaybackQualityCapturedReportPath.GetReportRelativePath(runId);
            var qualityRunFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(
                "quality-run",
                CreationCollisionOption.OpenIfExists);
            var capturedFolder = await qualityRunFolder.CreateFolderAsync(
                "captured",
                CreationCollisionOption.OpenIfExists);
            var folder = await EnsureQualityRunFolderPathAsync(capturedFolder, reportRelativePath);
            var fileName = GetQualityRunFileName(reportRelativePath);
            var file = await folder.CreateFileAsync(
                fileName,
                CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, PlaybackQualityReportSerializer.Serialize(result));
            return "quality-run/captured/" + reportRelativePath;
        }

        private async Task WriteQualityRunErrorReportAsync(Exception exception)
        {
            var request = _launchRequest;
            if (request == null || !request.IsQualityRun)
            {
                return;
            }

            try
            {
                var referenceCase = PlaybackQualityCaptureReferenceCaseFactory.Create(
                    request.QualityRunId,
                    request.ItemId,
                    request.MediaSourceId,
                    request.StartPositionTicks,
                    request.ForceSdrOutput,
                    request.QualityExpected,
                    request.QualityScenario,
                    uri: request.QualitySourceLocator);
                var result = PlaybackQualityRuntimeEvidenceCollector.ComposeErrorRunResult(
                    referenceCase,
                    new PlaybackQualityError
                    {
                        Code = "app-hosted-quality-run.playback-command-failed",
                        Message = exception.Message ?? "",
                        Operation = "quality-run-open",
                        ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                        FailureClass = "needs human confirmation",
                        FailureArea = "error-handling",
                        IsTerminal = true,
                        IsRetriable = false
                    },
                    new PlaybackQualityEnvironment
                    {
                        CollectorVersion = "app-hosted-quality-run-v0.1",
                        PlayerCoreVersion = "NoiraPlayer.Core",
                        SourceRevision = request.QualitySourceRevision,
                        BuildConfiguration = "Debug"
                    },
                    CreateAppHostedExecutionEvidence(
                        referenceCase,
                        request.QualityCommandReceivedAtUtc,
                        PlaybackQualityExecutionStatus.Failed,
                        _orchestrator.CurrentDescriptor != null,
                        metricsProvider: null));
                var relativePath = await WriteQualityRunReportAsync(request.QualityRunId, result);
                await WriteQualityRunCommandResultAsync("capture-error", relativePath);
            }
            catch (Exception writeException)
            {
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "QualityRun error report exception " +
                    writeException.GetType().FullName +
                    " " +
                    writeException.Message);
            }
        }

        private static PlaybackQualityExecutionEvidence CreateAppHostedExecutionEvidence(
            PlaybackQualityReferenceCase referenceCase,
            DateTimeOffset startedAt,
            string status,
            bool sourceOpened,
            IPlaybackQualityMetricsProvider? metricsProvider)
        {
            var decodedVideoFrames = 0UL;
            var renderedVideoFrames = 0UL;
            if (metricsProvider != null &&
                metricsProvider.TryGetQualityMetrics(out var metrics) &&
                metrics != null)
            {
                decodedVideoFrames = metrics.DecodedVideoFrames;
                renderedVideoFrames = metrics.RenderedVideoFrames;
            }

            var decoderOpened = decodedVideoFrames > 0;
            var playbackSampleObserved = decoderOpened && renderedVideoFrames > 0;
            var locatorHash = PlaybackQualitySourceFingerprint.Compute(referenceCase.Uri);
            var sourceOpenAttempted =
                status == PlaybackQualityExecutionStatus.Completed || sourceOpened;

            return new PlaybackQualityExecutionEvidence
            {
                AttemptId = Guid.NewGuid().ToString("N"),
                Runner = "app-hosted",
                Scenario = referenceCase.ExecutionRequirement.Scenario,
                EvidenceLevel = PlaybackQualityEvidenceLevel.AppHosted,
                Status = status,
                SourceLocatorHash = locatorHash,
                OpenedSourceHash = "",
                StartedAtUtc = startedAt.ToString("O"),
                DurationMs = Math.Max(0, (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds),
                SourceOpenAttempted = sourceOpenAttempted,
                SourceOpened = sourceOpened,
                NativeGraphOpened = sourceOpened,
                DemuxStarted = sourceOpened,
                DecoderOpened = decoderOpened,
                PlaybackSampleObserved = playbackSampleObserved
            };
        }

        private static async Task<StorageFolder> EnsureQualityRunFolderPathAsync(
            StorageFolder root,
            string relativePath)
        {
            var folder = root;
            var segments = relativePath.Split('/');
            for (var index = 0; index < segments.Length - 1; index++)
            {
                folder = await folder.CreateFolderAsync(
                    segments[index],
                    CreationCollisionOption.OpenIfExists);
            }

            return folder;
        }

        private static string GetQualityRunFileName(string relativePath)
        {
            var segments = relativePath.Split('/');
            return segments[segments.Length - 1];
        }

        private static async Task WriteQualityRunCommandResultAsync(
            string status,
            string detail)
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    "dev-command-result.txt",
                    CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, status + Environment.NewLine + (detail ?? ""));
            }
            catch
            {
            }
        }

        #endif

#if DEBUG
        private static IReadOnlyList<EmbyMediaSource> ApplyForcedSdrOutputForDiagnostics(IReadOnlyList<EmbyMediaSource> sources)
        {
            return sources.Select(CloneForSdrOutputDiagnostics).ToList();
        }

        private static EmbyMediaSource CloneForSdrOutputDiagnostics(EmbyMediaSource source)
        {
            var clone = new EmbyMediaSource
            {
                Id = source.Id,
                Name = source.Name,
                Container = source.Container,
                Bitrate = source.Bitrate,
                Width = source.Width,
                Height = source.Height,
                HdrProfile = HdrPlaybackProfile.Sdr(),
                DirectStreamUrl = source.DirectStreamUrl,
                PlaySessionId = source.PlaySessionId
            };

            foreach (var stream in source.Streams)
            {
                clone.Streams.Add(stream);
            }

            return clone;
        }
#endif

        private static async Task LogXboxVideoCapabilitiesAsync()
        {
            try
            {
                var capabilities = new ProtectionCapabilities();
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "Xbox video capability hevcDecode=" +
                    QueryProtectionCapability(
                        capabilities,
                        "video/mp4;codecs=\"hvc1,mp4a\";features=\"decode-res-x=3840,decode-res-y=2160,decode-bitrate=20000,decode-fps=30,decode-bpc=10\""));
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "Xbox video capability hevc4kDisplay=" +
                    QueryProtectionCapability(
                        capabilities,
                        "video/mp4;codecs=\"hvc1,mp4a\";features=\"decode-res-x=3840,decode-res-y=2160,decode-bitrate=20000,decode-fps=30,decode-bpc=10,display-res-x=3840,display-res-y=2160,display-bpc=8\""));
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "Xbox video capability hevc4kHdr=" +
                    QueryProtectionCapability(
                        capabilities,
                        "video/mp4;codecs=\"hvc1,mp4a\";features=\"decode-res-x=3840,decode-res-y=2160,decode-bitrate=20000,decode-fps=30,decode-bpc=10,display-res-x=3840,display-res-y=2160,display-bpc=10,hdr=1\""));
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "Xbox video capability hdcp2=" +
                    QueryProtectionCapability(
                        capabilities,
                        "video/mp4;codecs=\"hvc1,mp4a\";features=\"hdcp=2\""));
            }
            catch (Exception ex)
            {
                await PlaybackDiagnosticsLog.WriteLineAsync(
                    "Xbox video capability exception " + ex.GetType().FullName + " " + ex.Message);
            }
        }

        private static ProtectionCapabilityResult QueryProtectionCapability(
            ProtectionCapabilities capabilities,
            string type)
        {
            return capabilities.IsTypeSupported(type, "com.microsoft.playready.hardware");
        }

        private async Task StopPlaybackAsync()
        {
            var stoppedRequest = CreateStoppedSessionRequest();
            await _orchestrator.StopAsync();
            await ReportPlaybackStoppedAsync(stoppedRequest);
            _progressTimer.Stop();
            _lastPositionTicks = 0;
            _durationTicks = 0;
            _hasPlaybackContext = false;
            _lastPlaybackSessionRequest = null;
            UpdateStatus(CorePlaybackState.Stopped);
            UpdateProgressSlider();
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
            if (!IsPlaybackSeekable())
            {
                ShowPlaybackNotReady();
                return;
            }

            var current = TimeSpan.FromTicks(GetCurrentPositionTicks());
            var target = current + delta;
            if (target < TimeSpan.Zero)
            {
                target = TimeSpan.Zero;
            }

            var targetTicks = ClampToDuration(target.Ticks);
            await _orchestrator.SeekAsync(targetTicks);
            _lastPositionTicks = targetTicks;
            await ReportProgressAsync(PlaybackProgressEvent.TimeUpdate);
            UpdateStatus(_orchestrator.State, "Position " + FormatPosition(TimeSpan.FromTicks(targetTicks)));
            UpdateProgressSlider();
            ShowOverlay();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task RunPlaybackCommandAsync(Func<Task> command)
        {
            if (_playbackCommandInFlight)
            {
                return;
            }

            _playbackCommandInFlight = true;
            _overlayTimer.Stop();
            try
            {
                await PlaybackDiagnosticsLog.WriteLineAsync("Playback command begin");
                await command();
                await PlaybackDiagnosticsLog.WriteLineAsync("Playback command end");
            }
            catch (Exception ex)
            {
                await PlaybackDiagnosticsLog.WriteLineAsync("Playback command exception " + ex.GetType().FullName + " " + ex.Message);
#if DEBUG
                await WriteQualityRunErrorReportAsync(ex);
#endif
                _hasPlaybackContext = _orchestrator.CurrentDescriptor != null;
                UpdateStatus(CorePlaybackState.Failed, ex.Message);
                ShowOverlay();
                UpdateControlStates();
                UpdateStreamControlStates();
                if (_infoVisible)
                {
                    UpdateInfo();
                }
            }
            finally
            {
                _playbackCommandInFlight = false;
                if (_overlayVisible)
                {
                    _overlayTimer.Stop();
                    if (!ShouldKeepOverlayPinned())
                    {
                        _overlayTimer.Start();
                    }
                }

                await PlaybackDiagnosticsLog.WriteLineAsync("Playback command finally");
            }
        }

        private static string GetLaunchRequestDisplayName(PlaybackLaunchRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ItemId))
            {
                return request.ItemId;
            }

            if (!string.IsNullOrWhiteSpace(request.QualityRunId))
            {
                return request.QualityRunId;
            }

            return "Direct Stream";
        }

        private static string GetDirectStreamQualityRunItemId(PlaybackLaunchRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ItemId))
            {
                return request.ItemId;
            }

            if (!string.IsNullOrWhiteSpace(request.QualityRunId))
            {
                return request.QualityRunId;
            }

            return DemoItemId;
        }

        private static EmbyMediaSource CreateDirectStreamQualityRunSource(PlaybackLaunchRequest request)
        {
            var expected = request.QualityExpected;
            var frameRate = expected != null && expected.FrameRate > 0
                ? expected.FrameRate
                : 0;
            var sourceId = string.IsNullOrWhiteSpace(request.MediaSourceId)
                ? "direct-uri"
                : request.MediaSourceId;
            var source = new EmbyMediaSource
            {
                Id = sourceId,
                Name = "Direct Stream Quality Run",
                DirectStreamUrl = request.DirectStreamUrl,
                RunTimeTicks = request.RuntimeTicks,
                VideoFrameRate = frameRate,
                Width = expected == null ? 0 : expected.Width,
                Height = expected == null ? 0 : expected.Height,
                HdrProfile = CreateQualityRunHdrProfile(expected)
            };
            source.Streams.Add(new EmbyMediaStream
            {
                Index = 0,
                Kind = EmbyStreamKind.Video,
                Codec = expected == null ? "" : expected.Codec,
                DisplayTitle = "Direct stream quality run",
                VideoRange = expected == null ? "" : expected.VideoRange,
                ColorPrimaries = expected == null ? "" : expected.ColorPrimaries,
                ColorTransfer = expected == null ? "" : expected.ColorTransfer,
                ColorSpace = expected == null ? "" : expected.ColorSpace,
                RealFrameRate = frameRate,
                AverageFrameRate = frameRate
            });

            return source;
        }

        private static HdrPlaybackProfile CreateQualityRunHdrProfile(PlaybackQualityExpected? expected)
        {
            if (expected == null)
            {
                return HdrPlaybackProfile.Sdr();
            }

            return new HdrPlaybackProfile
            {
                Kind = ParseQualityRunHdrKind(expected.HdrKind),
                VideoRange = expected.VideoRange,
                ColorPrimaries = expected.ColorPrimaries,
                ColorTransfer = expected.ColorTransfer,
                ColorSpace = expected.ColorSpace,
                Codec = expected.Codec,
                IsDolbyVision = expected.IsDolbyVision == true,
                DolbyVisionProfile = expected.DolbyVisionProfile,
                DolbyVisionCompatibilityId = expected.DolbyVisionCompatibilityId,
                HasHdr10BaseLayer = expected.HasHdr10BaseLayer == true,
                HasHlgBaseLayer = expected.HasHlgBaseLayer == true
            };
        }

        private static HdrPlaybackKind ParseQualityRunHdrKind(string hdrKind)
        {
            if (string.Equals(hdrKind, "Hdr10", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.Hdr10;
            }

            if (string.Equals(hdrKind, "Hlg", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.Hlg;
            }

            if (string.Equals(hdrKind, "DolbyVisionWithHdr10Fallback", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.DolbyVisionWithHdr10Fallback;
            }

            if (string.Equals(hdrKind, "DolbyVisionWithHlgFallback", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.DolbyVisionWithHlgFallback;
            }

            if (string.Equals(hdrKind, "DolbyVisionUnsupported", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.DolbyVisionUnsupported;
            }

            if (string.Equals(hdrKind, "UnknownHdr", StringComparison.Ordinal))
            {
                return HdrPlaybackKind.UnknownHdr;
            }

            return HdrPlaybackKind.Sdr;
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

        private async void ProgressTimer_OnTick(object? sender, object e)
        {
            await ReportProgressAsync(PlaybackProgressEvent.TimeUpdate);
            UpdateProgressSlider();
        }

        private void OverlayTimer_OnTick(object? sender, object e)
        {
            if (ShouldKeepOverlayPinned())
            {
                _overlayTimer.Stop();
                return;
            }

            HideOverlay();
        }

        private async void SeekPreviewTimer_OnTick(object? sender, object e)
        {
            var decision = _seekPreview.DecideTimeout(GetSeekPreviewNow());
            if (decision.Kind == SeekPreviewDecisionKind.None)
            {
                return;
            }

            await CompleteSeekPreviewDecisionAsync(decision);
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
                var progress = _orchestrator.CreateProgressRequest(eventName);
                _lastPlaybackSessionRequest = CloneSessionRequest(progress);
                await _embyClient.ReportProgressAsync(
                    _session,
                    progress);
            }
            catch
            {
            }
            finally
            {
                _reportInProgress = false;
            }
        }

        private async Task ReportPlaybackStartedAsync()
        {
            if (_embyClient == null ||
                _session == null ||
                _orchestrator.CurrentDescriptor == null)
            {
                return;
            }

            try
            {
                var request = _orchestrator.CreateSessionRequest();
                _lastPlaybackSessionRequest = CloneSessionRequest(request);
                await _embyClient.ReportPlaybackStartAsync(_session, request);
            }
            catch
            {
            }
        }

        private async Task ReportPlaybackStoppedAsync(PlaybackSessionRequest? stoppedRequest = null)
        {
            if (_stopReportInProgress ||
                _playbackStoppedReported ||
                _embyClient == null ||
                _session == null)
            {
                return;
            }

            var request = stoppedRequest ?? CreateStoppedSessionRequest();
            if (request == null)
            {
                return;
            }

            try
            {
                _stopReportInProgress = true;
                _playbackStoppedReported = true;
                await _embyClient.ReportPlaybackStoppedAsync(_session, request);
            }
            catch
            {
            }
            finally
            {
                _stopReportInProgress = false;
            }
        }

        private PlaybackSessionRequest? CreateStoppedSessionRequest()
        {
            PlaybackSessionRequest? request = null;
            if (_orchestrator.CurrentDescriptor != null)
            {
                request = _orchestrator.CreateSessionRequest();
            }
            else if (_lastPlaybackSessionRequest != null)
            {
                request = CloneSessionRequest(_lastPlaybackSessionRequest);
            }

            if (request == null)
            {
                return null;
            }

            request.PositionTicks = GetCurrentPositionTicks();
            request.IsPaused = false;
            _lastPlaybackSessionRequest = CloneSessionRequest(request);
            return request;
        }

        private static PlaybackSessionRequest CloneSessionRequest(PlaybackSessionRequest request)
        {
            return new PlaybackSessionRequest
            {
                ItemId = request.ItemId,
                MediaSourceId = request.MediaSourceId,
                PlaySessionId = request.PlaySessionId,
                PositionTicks = request.PositionTicks,
                IsPaused = request.IsPaused,
                PlayMethod = request.PlayMethod,
                AudioStreamIndex = request.AudioStreamIndex,
                SubtitleStreamIndex = request.SubtitleStreamIndex
            };
        }

        private void UpdateStatus(CorePlaybackState state, string message = "")
        {
            StatusBlock.Text = string.IsNullOrWhiteSpace(message)
                ? state.ToString()
                : state + " - " + message;
        }

        private void ShowOverlay()
        {
            ShowOverlay(_moreVisible);
        }

        private void ShowOverlay(bool showMore)
        {
            ShowOverlay(showMore, false);
        }

        private void ShowOverlay(bool showMore, bool preserveMoreDrawerFocus)
        {
            var wasOverlayVisible = _overlayVisible;
            if (!_overlayVisible)
            {
                _overlayVisible = true;
                OverlayRoot.Visibility = Visibility.Visible;
            }

            _moreVisible = showMore;
            MoreDrawer.Visibility = _moreVisible ? Visibility.Visible : Visibility.Collapsed;
            if (_moreVisible)
            {
                ClearTransportFocusForMoreDrawer();
                if (!preserveMoreDrawerFocus)
                {
                    FocusMoreDrawer();
                }
            }
            else
            {
                FocusTransportForOverlay(wasOverlayVisible);
            }

            _overlayTimer.Stop();
            if (!ShouldKeepOverlayPinned())
            {
                _overlayTimer.Start();
            }
        }

        private void ClearTransportFocusForMoreDrawer()
        {
            _transportFocusTarget = null;
            UpdateTransportFocusVisuals(null);
        }

        private void KeepOverlayVisibleIfPinned()
        {
            if (!ShouldKeepOverlayPinned())
            {
                return;
            }

            if (!_overlayVisible)
            {
                _overlayVisible = true;
                OverlayRoot.Visibility = Visibility.Visible;
            }

            _overlayTimer.Stop();
        }

        private void CloseMoreDrawer()
        {
            _moreVisible = false;
            _moreDrawerFocusTarget = null;
            UpdateMoreDrawerFocusVisuals(null);
            MoreDrawer.Visibility = Visibility.Collapsed;
            InfoPanel.Visibility = Visibility.Collapsed;
            _infoVisible = false;
            FocusTransportTarget(PlaybackTransportFocusTarget.More);

            _overlayTimer.Stop();
            if (!ShouldKeepOverlayPinned())
            {
                _overlayTimer.Start();
            }
        }

        private void FocusTransportForOverlay(bool wasOverlayVisible)
        {
            if (!wasOverlayVisible ||
                !_transportFocusTarget.HasValue ||
                !IsTransportTargetEnabled(_transportFocusTarget.Value))
            {
                FocusTransportTarget(GetDefaultTransportFocusTarget());
                return;
            }

            FocusTransportTarget(_transportFocusTarget.Value);
        }

        private PlaybackTransportFocusTarget GetDefaultTransportFocusTarget()
        {
            return PlaybackTransportFocusPolicy.GetDefaultTarget(
                _orchestrator.State,
                PauseButton.IsEnabled,
                ResumeButton.IsEnabled,
                SeekBackButton.IsEnabled,
                SeekForwardButton.IsEnabled,
                MoreButton.IsEnabled,
                StopButton.IsEnabled);
        }

        private void FocusTransportTarget(PlaybackTransportFocusTarget target)
        {
            if (!IsTransportTargetEnabled(target))
            {
                target = GetDefaultTransportFocusTarget();
            }

            _transportFocusTarget = target;
            UpdateTransportFocusVisuals(target);
            switch (target)
            {
                case PlaybackTransportFocusTarget.Pause:
                    TryFocusTransportControl(PauseButton);
                    return;

                case PlaybackTransportFocusTarget.Resume:
                    TryFocusTransportControl(ResumeButton);
                    return;

                case PlaybackTransportFocusTarget.SeekBack:
                    TryFocusTransportControl(SeekBackButton);
                    return;

                case PlaybackTransportFocusTarget.SeekForward:
                    TryFocusTransportControl(SeekForwardButton);
                    return;

                case PlaybackTransportFocusTarget.More:
                    TryFocusTransportControl(MoreButton);
                    return;

                case PlaybackTransportFocusTarget.Stop:
                    TryFocusTransportControl(StopButton);
                    return;
            }
        }

        private static bool TryFocusTransportControl(Control control)
        {
            if (!control.IsEnabled)
            {
                return false;
            }

            return control.Focus(FocusState.Keyboard) ||
                control.Focus(FocusState.Programmatic);
        }

        private void UpdateTransportFocusVisuals(PlaybackTransportFocusTarget? target)
        {
            SetTransportControlVisual(PauseButton, target == PlaybackTransportFocusTarget.Pause);
            SetTransportControlVisual(ResumeButton, target == PlaybackTransportFocusTarget.Resume);
            SetTransportControlVisual(SeekBackButton, target == PlaybackTransportFocusTarget.SeekBack);
            SetTransportControlVisual(SeekForwardButton, target == PlaybackTransportFocusTarget.SeekForward);
            SetTransportControlVisual(MoreButton, target == PlaybackTransportFocusTarget.More);
            SetTransportControlVisual(StopButton, target == PlaybackTransportFocusTarget.Stop);
        }

        private static void SetTransportControlVisual(Control control, bool isFocused)
        {
            var resources = Application.Current.Resources;
            control.BorderBrush = (Brush)resources[isFocused ? "AppFocusedCardFillBrush" : "AppTransparentBrush"];
            control.BorderThickness = new Thickness(1);
            control.Background = (Brush)resources[isFocused ? "AppFocusedCardFillBrush" : "AppChromeBrush"];
        }

        private PlaybackTransportFocusTarget? GetFocusedTransportTarget()
        {
            var focusedElement = FocusManager.GetFocusedElement();
            if (ReferenceEquals(focusedElement, PauseButton))
            {
                _transportFocusTarget = PlaybackTransportFocusTarget.Pause;
                return _transportFocusTarget;
            }

            if (ReferenceEquals(focusedElement, ResumeButton))
            {
                _transportFocusTarget = PlaybackTransportFocusTarget.Resume;
                return _transportFocusTarget;
            }

            if (ReferenceEquals(focusedElement, SeekBackButton))
            {
                _transportFocusTarget = PlaybackTransportFocusTarget.SeekBack;
                return _transportFocusTarget;
            }

            if (ReferenceEquals(focusedElement, SeekForwardButton))
            {
                _transportFocusTarget = PlaybackTransportFocusTarget.SeekForward;
                return _transportFocusTarget;
            }

            if (ReferenceEquals(focusedElement, MoreButton))
            {
                _transportFocusTarget = PlaybackTransportFocusTarget.More;
                return _transportFocusTarget;
            }

            if (ReferenceEquals(focusedElement, StopButton))
            {
                _transportFocusTarget = PlaybackTransportFocusTarget.Stop;
                return _transportFocusTarget;
            }

            return _transportFocusTarget;
        }

        private bool IsTransportTargetEnabled(PlaybackTransportFocusTarget target)
        {
            switch (target)
            {
                case PlaybackTransportFocusTarget.Pause:
                    return PauseButton.IsEnabled;

                case PlaybackTransportFocusTarget.Resume:
                    return ResumeButton.IsEnabled;

                case PlaybackTransportFocusTarget.SeekBack:
                    return SeekBackButton.IsEnabled;

                case PlaybackTransportFocusTarget.SeekForward:
                    return SeekForwardButton.IsEnabled;

                case PlaybackTransportFocusTarget.More:
                    return MoreButton.IsEnabled;

                case PlaybackTransportFocusTarget.Stop:
                    return StopButton.IsEnabled;

                default:
                    return false;
            }
        }

        private async Task ActivateFocusedTransportControlAsync()
        {
            var target = GetFocusedTransportTarget() ?? GetDefaultTransportFocusTarget();
            if (!IsTransportTargetEnabled(target))
            {
                target = GetDefaultTransportFocusTarget();
                FocusTransportTarget(target);
            }

            switch (target)
            {
                case PlaybackTransportFocusTarget.Pause:
                    await RunPlaybackCommandAsync(PausePlaybackAsync);
                    return;

                case PlaybackTransportFocusTarget.Resume:
                    await RunPlaybackCommandAsync(ResumePlaybackAsync);
                    return;

                case PlaybackTransportFocusTarget.SeekBack:
                    await RunPlaybackCommandAsync(() => SeekRelativeAsync(-SeekBackStep));
                    return;

                case PlaybackTransportFocusTarget.SeekForward:
                    await RunPlaybackCommandAsync(() => SeekRelativeAsync(SeekForwardStep));
                    return;

                case PlaybackTransportFocusTarget.More:
                    ShowOverlay(true);
                    return;

                case PlaybackTransportFocusTarget.Stop:
                    await RunPlaybackCommandAsync(StopPlaybackAsync);
                    return;
            }
        }

        private void RestartOverlayTimerIfNeeded()
        {
            if (!_overlayVisible)
            {
                return;
            }

            _overlayTimer.Stop();
            if (!ShouldKeepOverlayPinned())
            {
                _overlayTimer.Start();
            }
        }

        private void FocusMoreDrawer()
        {
            var target = PlaybackMoreDrawerFocusPolicy.GetDefaultTarget(
                SourceBox.IsEnabled,
                AudioStreamBox.IsEnabled,
                SubtitleStreamBox.IsEnabled);
            _moreDrawerFocusTarget = target;
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_moreVisible)
                {
                    FocusMoreDrawerTarget(target);
                }
            });
        }

        private void FocusMoreDrawerTarget(PlaybackMoreDrawerFocusTarget target)
        {
            _moreDrawerFocusTarget = target;
            UpdateMoreDrawerFocusVisuals(target);
            switch (target)
            {
                case PlaybackMoreDrawerFocusTarget.Source:
                    if (TryFocusMoreDrawerControl(SourceBox))
                    {
                        return;
                    }

                    break;

                case PlaybackMoreDrawerFocusTarget.Audio:
                    if (TryFocusMoreDrawerControl(AudioStreamBox))
                    {
                        return;
                    }

                    break;

                case PlaybackMoreDrawerFocusTarget.Subtitles:
                    if (TryFocusMoreDrawerControl(SubtitleStreamBox))
                    {
                        return;
                    }

                    break;

                case PlaybackMoreDrawerFocusTarget.Info:
                    if (TryFocusMoreDrawerControl(InfoButton))
                    {
                        return;
                    }

                    break;
            }

            _moreDrawerFocusTarget = PlaybackMoreDrawerFocusTarget.Info;
            UpdateMoreDrawerFocusVisuals(_moreDrawerFocusTarget);
            TryFocusMoreDrawerControl(InfoButton);
        }

        private static bool TryFocusMoreDrawerControl(Control control)
        {
            if (!control.IsEnabled)
            {
                return false;
            }

            return control.Focus(FocusState.Keyboard) ||
                control.Focus(FocusState.Programmatic);
        }

        private void UpdateMoreDrawerFocusVisuals(PlaybackMoreDrawerFocusTarget? target)
        {
            SetMoreDrawerControlVisual(SourceBox, target == PlaybackMoreDrawerFocusTarget.Source);
            SetMoreDrawerControlVisual(AudioStreamBox, target == PlaybackMoreDrawerFocusTarget.Audio);
            SetMoreDrawerControlVisual(SubtitleStreamBox, target == PlaybackMoreDrawerFocusTarget.Subtitles);
            SetMoreDrawerControlVisual(InfoButton, target == PlaybackMoreDrawerFocusTarget.Info);
        }

        private static void SetMoreDrawerControlVisual(Control control, bool isFocused)
        {
            var resources = Application.Current.Resources;
            control.BorderBrush = (Brush)resources["AppTransparentBrush"];
            control.BorderThickness = control is ComboBox ? new Thickness(0) : new Thickness(1);
            control.Background = (Brush)resources[isFocused ? "AppFocusedCardFillBrush" : "AppChromeBrush"];
        }

        private PlaybackMoreDrawerFocusTarget? GetFocusedMoreDrawerTarget()
        {
            var focusedElement = FocusManager.GetFocusedElement();
            if (ReferenceEquals(focusedElement, SourceBox))
            {
                _moreDrawerFocusTarget = PlaybackMoreDrawerFocusTarget.Source;
                return _moreDrawerFocusTarget;
            }

            if (ReferenceEquals(focusedElement, AudioStreamBox))
            {
                _moreDrawerFocusTarget = PlaybackMoreDrawerFocusTarget.Audio;
                return _moreDrawerFocusTarget;
            }

            if (ReferenceEquals(focusedElement, SubtitleStreamBox))
            {
                _moreDrawerFocusTarget = PlaybackMoreDrawerFocusTarget.Subtitles;
                return _moreDrawerFocusTarget;
            }

            if (ReferenceEquals(focusedElement, InfoButton))
            {
                _moreDrawerFocusTarget = PlaybackMoreDrawerFocusTarget.Info;
                return _moreDrawerFocusTarget;
            }

            return _moreDrawerFocusTarget;
        }

        private bool IsAnyMoreDrawerComboBoxOpen()
        {
            return SourceBox.IsDropDownOpen ||
                AudioStreamBox.IsDropDownOpen ||
                SubtitleStreamBox.IsDropDownOpen;
        }

        private void HideOverlay()
        {
            if (!_overlayVisible || _moreVisible)
            {
                return;
            }

            _overlayTimer.Stop();
            _overlayVisible = false;
            OverlayRoot.Visibility = Visibility.Collapsed;
            MoreDrawer.Visibility = Visibility.Collapsed;
            SeekPreviewBlock.Visibility = Visibility.Collapsed;
            _infoVisible = false;
            _transportFocusTarget = null;
            UpdateTransportFocusVisuals(null);
            Focus(FocusState.Programmatic);
        }

        private void BeginOrMoveSeekPreview(TimeSpan delta)
        {
            var now = GetSeekPreviewNow();
            if (!_seekPreview.IsActive)
            {
                _seekPreview.Begin(GetCurrentPositionTicks(), now);
            }

            _seekPreview.MoveBy(delta, now);
            ShowOverlay();
            UpdateSeekPreviewBlock();
            UpdateProgressSlider();
            _seekPreviewTimer.Stop();
            _seekPreviewTimer.Start();
        }

        private void BeginOrMoveSeekPreviewTo(long positionTicks)
        {
            var now = GetSeekPreviewNow();
            if (!_seekPreview.IsActive)
            {
                _seekPreview.Begin(GetCurrentPositionTicks(), now);
            }

            _seekPreview.MoveTo(ClampToDuration(positionTicks), now);
            ShowOverlay();
            UpdateSeekPreviewBlock();
            UpdateProgressSlider();
            _seekPreviewTimer.Stop();
            _seekPreviewTimer.Start();
        }

        private async Task CompleteSeekPreviewDecisionAsync(SeekPreviewDecision decision)
        {
            if (decision.Kind == SeekPreviewDecisionKind.None)
            {
                return;
            }

            _seekPreviewTimer.Stop();
            SeekPreviewBlock.Visibility = Visibility.Collapsed;

            if (decision.Kind == SeekPreviewDecisionKind.Commit)
            {
                if (!IsPlaybackSeekable())
                {
                    ShowPlaybackNotReady();
                    return;
                }

                if (_playbackCommandInFlight)
                {
                    ShowOverlay();
                    UpdateStatus(_orchestrator.State, "Playback busy");
                    UpdateProgressSlider();
                    if (_infoVisible)
                    {
                        UpdateInfo();
                    }

                    return;
                }

                await RunPlaybackCommandAsync(() => CommitSeekPreviewAsync(decision.PositionTicks));
                return;
            }

            UpdateStatus(_orchestrator.State, "Seek canceled");
            UpdateProgressSlider();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private async Task CommitSeekPreviewAsync(long positionTicks)
        {
            await _orchestrator.SeekAsync(positionTicks);
            _lastPositionTicks = Math.Max(0, positionTicks);
            await ReportProgressAsync(PlaybackProgressEvent.TimeUpdate);
            UpdateStatus(_orchestrator.State, "Position " + FormatPosition(TimeSpan.FromTicks(_lastPositionTicks)));
            UpdateProgressSlider();
            if (_infoVisible)
            {
                UpdateInfo();
            }
        }

        private void UpdateSeekPreviewBlock()
        {
            SeekPreviewBlock.Text = PlaybackSeekPreviewPrompt.Format(
                TimeSpan.FromTicks(Math.Max(0, _seekPreview.TargetTicks)));
            SeekPreviewBlock.Visibility = Visibility.Visible;
        }

        private void ClearSeekPreview()
        {
            if (_seekPreview.IsActive)
            {
                _seekPreview.Cancel();
            }

            _seekPreviewTimer.Stop();
            SeekPreviewBlock.Visibility = Visibility.Collapsed;
        }

        private bool CanAcceptSeekInput()
        {
            if (!IsPlaybackSeekable())
            {
                ShowPlaybackNotReady();
                return false;
            }

            if (_playbackCommandInFlight)
            {
                ShowOverlay();
                return false;
            }

            return true;
        }

        private bool IsPlaybackSeekable()
        {
            var state = _orchestrator.State;
            return _orchestrator.CurrentDescriptor != null &&
                state != CorePlaybackState.Stopped &&
                state != CorePlaybackState.Failed &&
                state != CorePlaybackState.Opening;
        }

        private void ShowPlaybackNotReady()
        {
            ClearSeekPreview();
            ShowOverlay();
            UpdateStatus(_orchestrator.State, "Playback is not ready");
        }

        private void UpdateProgressSlider()
        {
            var positionTicks = _seekPreview.IsActive
                ? _seekPreview.TargetTicks
                : GetCurrentPositionTicks();
            var positionSeconds = Math.Max(0, TimeSpan.FromTicks(positionTicks).TotalSeconds);
            var durationSeconds = _durationTicks > 0
                ? TimeSpan.FromTicks(_durationTicks).TotalSeconds
                : Math.Max(ProgressSlider.Maximum, positionSeconds);

            _updatingProgressSlider = true;
            try
            {
                ProgressSlider.Maximum = Math.Max(1, durationSeconds);
                ProgressSlider.Value = Math.Min(ProgressSlider.Maximum, positionSeconds);
                CurrentTimeBlock.Text = FormatPosition(TimeSpan.FromSeconds(positionSeconds));
                DurationBlock.Text = _durationTicks > 0
                    ? FormatPosition(TimeSpan.FromTicks(_durationTicks))
                    : "--:--";
            }
            finally
            {
                _updatingProgressSlider = false;
            }
        }

        private bool ShouldKeepOverlayPinned()
        {
            return PlaybackOverlayInputPolicy.ShouldKeepOverlayPinned(
                _moreVisible,
                _seekPreview.IsActive,
                _launchRequest == null && ManualDebugPanel.Visibility == Visibility.Visible,
                IsPlaybackOpeningOrBusy(),
                PlaybackNeedsAttention());
        }

        private bool IsPlaybackOpeningOrBusy()
        {
            return _playbackCommandInFlight || _orchestrator.State == CorePlaybackState.Opening;
        }

        private bool PlaybackNeedsAttention()
        {
            return _orchestrator.State == CorePlaybackState.Failed;
        }

        private void UpdateControlStates()
        {
            var state = _orchestrator.State;
            var hasActivePlayback = _hasPlaybackContext &&
                state != CorePlaybackState.Failed &&
                state != CorePlaybackState.Stopped;

            ManualStartButton.IsEnabled = _launchRequest == null && IsSupportedDirectStreamUrl(StreamUrlBox.Text);
            PauseButton.IsEnabled = hasActivePlayback && state != CorePlaybackState.Paused;
            ResumeButton.IsEnabled = hasActivePlayback && state == CorePlaybackState.Paused;
            StopButton.IsEnabled = hasActivePlayback;
            SeekBackButton.IsEnabled = hasActivePlayback;
            SeekForwardButton.IsEnabled = hasActivePlayback;
            ProgressSlider.IsEnabled = hasActivePlayback && IsPlaybackSeekable();
            MoreButton.IsEnabled = true;
            InfoButton.IsEnabled = true;
            UpdatePlaybackOptionChips();

            if (_overlayVisible && !_moreVisible)
            {
                if (!_transportFocusTarget.HasValue ||
                    !IsTransportTargetEnabled(_transportFocusTarget.Value))
                {
                    FocusTransportTarget(GetDefaultTransportFocusTarget());
                }
                else
                {
                    UpdateTransportFocusVisuals(_transportFocusTarget);
                }
            }
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
                    "Position: " + FormatPosition(position) + Environment.NewLine +
                    CreateDisplayDiagnosticLines();
                return;
            }

            InfoBlock.Text =
                "State: " + _orchestrator.State + Environment.NewLine +
                "Item: " + (string.IsNullOrWhiteSpace(_currentItemName) ? descriptor.ItemId : _currentItemName) + Environment.NewLine +
                "Source: " + source.Name + Environment.NewLine +
                "HDR strategy: " + source.HdrProfile.PlaybackStrategy + Environment.NewLine +
                "Audio: " + CreateSelectedStreamLabel(source, descriptor.AudioStreamIndex, EmbyStreamKind.Audio, "Default") + Environment.NewLine +
                "Subtitles: " + CreateSelectedStreamLabel(source, descriptor.SubtitleStreamIndex, EmbyStreamKind.Subtitle, "Off") + Environment.NewLine +
                "Position: " + FormatPosition(position) + Environment.NewLine +
                CreateDisplayDiagnosticLines() +
                "URL: " + source.DirectStreamUrl;
        }

        private void UpdatePlaybackOptionChips()
        {
            var descriptor = _orchestrator.CurrentDescriptor;
            var source = descriptor?.MediaSource;
            if (descriptor == null || source == null)
            {
                SourceChipBlock.Text = "Source";
                AudioChipBlock.Text = "Audio";
                SubtitleChipBlock.Text = "Subtitles";
                return;
            }

            SourceChipBlock.Text = source.Name;
            AudioChipBlock.Text = CreateSelectedStreamLabel(source, descriptor.AudioStreamIndex, EmbyStreamKind.Audio, "Default");
            SubtitleChipBlock.Text = CreateSelectedStreamLabel(source, descriptor.SubtitleStreamIndex, EmbyStreamKind.Subtitle, "Off");
        }

        private string CreateDisplayDiagnosticLines()
        {
            if (!(_backend is IPlaybackBackendDiagnostics diagnostics))
            {
                return "";
            }

            var status = diagnostics.DisplayStatus;
            var swapChainFormat = string.IsNullOrWhiteSpace(status.SwapChainFormat)
                ? "Unknown"
                : status.SwapChainFormat;
            var colorSpace = string.IsNullOrWhiteSpace(status.SwapChainColorSpace)
                ? "Unknown"
                : status.SwapChainColorSpace;
            var inputColorSpace = string.IsNullOrWhiteSpace(status.VideoProcessorInputColorSpace)
                ? "Unknown"
                : status.VideoProcessorInputColorSpace;
            var outputColorSpace = string.IsNullOrWhiteSpace(status.VideoProcessorOutputColorSpace)
                ? "Unknown"
                : status.VideoProcessorOutputColorSpace;
            var conversionStatus = string.IsNullOrWhiteSpace(status.VideoProcessorConversionStatus)
                ? "not-run"
                : status.VideoProcessorConversionStatus;

            var toneMappingLine = status.HasMissingToneMappingImplementation
                ? "Tone mapping: missing / not color-equivalent" + Environment.NewLine
                : status.RequiresExplicitToneMapping
                    ? "Tone mapping: required / not color-equivalent" + Environment.NewLine
                    : "";

            return
                "HDR output: " + status.HdrStatus + (status.IsHdrOutputActive ? " active" : "") + Environment.NewLine +
                "Swapchain: " + swapChainFormat + " / " + colorSpace + (status.IsTenBitSwapChain ? " / 10-bit" : " / 8-bit") + Environment.NewLine +
                "Video processor: " + conversionStatus + " / " + inputColorSpace + " -> " + outputColorSpace + Environment.NewLine +
                toneMappingLine;
        }

        private static string CreateSourceLabel(EmbyMediaSource source)
        {
            var label = string.IsNullOrWhiteSpace(source.Name) ? source.Id : source.Name;
            if (source.Width > 0 && source.Height > 0)
            {
                label += " · " + source.Width + "x" + source.Height;
            }

            if (source.HdrProfile.Kind != HdrPlaybackKind.Sdr)
            {
                label += " · " + source.HdrProfile.PlaybackStrategy;
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
            var backendPositionTicks = Math.Max(0, _backend.CurrentPositionTicks);
            if (_orchestrator.CurrentDescriptor != null && backendPositionTicks > 0)
            {
                _lastPositionTicks = backendPositionTicks;
                return backendPositionTicks;
            }

            return Math.Max(0, _lastPositionTicks);
        }

        private long ClampToDuration(long positionTicks)
        {
            var clamped = Math.Max(0, positionTicks);
            return _durationTicks > 0 ? Math.Min(_durationTicks, clamped) : clamped;
        }

        private static TimeSpan GetSeekPreviewNow()
        {
            return TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
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
