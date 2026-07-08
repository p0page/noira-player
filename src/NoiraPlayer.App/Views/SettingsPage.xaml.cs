using System;
using System.IO;
using System.Linq;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Services;
using NoiraPlayer.App.Storage;
using NoiraPlayer.Core.Diagnostics;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace NoiraPlayer.App.Views
{
    public sealed partial class SettingsPage : Page, ITvContentFocusTarget
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private readonly PlaybackPreferenceStore _playbackPreferences = new PlaybackPreferenceStore();
        private bool _diagnosticsExpanded;
        private bool _loadingSettings;
        private bool _signingOut;

        public SettingsPage()
        {
            InitializeComponent();
            PrepareSettingsUtilityVisuals();
            RegisterSettingsDirectionalFocusHandlers();
            Loaded += SettingsPage_OnLoaded;
        }

        private void PrepareSettingsUtilityVisuals()
        {
            MatteButtonFocusVisuals.PrepareCommandButton(SignOutButton);
            MatteButtonFocusVisuals.PrepareCommandButton(DiagnosticsToggleButton);
            MatteButtonFocusVisuals.PrepareCommandButton(CancelSignOutButton);
            MatteButtonFocusVisuals.PrepareDangerButton(ConfirmSignOutButton);
        }

        private async void SettingsPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingsPage_OnLoaded;
            _loadingSettings = true;

            var session = await _sessionStore.LoadAsync();
            if (session == null)
            {
                AccountBlock.Text = SettingsDiagnosticsFormatter.FormatAccount("", "");
            }
            else
            {
                AccountBlock.Text = SettingsDiagnosticsFormatter.FormatAccount(session.UserName, session.ServerUrl);
            }

            VersionBlock.Text = SettingsDiagnosticsFormatter.FormatVersionSummary(
                GetPackageVersion(),
                EmbyClientFactory.ClientVersion);
            ThumbstickSeekPreviewCheckBox.IsChecked = _playbackPreferences.IsThumbstickSeekPreviewEnabled();
            UpdateThumbstickSeekPreviewStatus();
            InputMapBlock.Text = "D-pad: arrow keys / A: Enter or Space / B: Escape / Menu: M";
            RenderStartupDiagnostics();
            UpdateDiagnosticsDisclosure();
            _loadingSettings = false;
            FocusDefaultContent();
        }

        public bool FocusDefaultContent()
        {
            return ThumbstickSeekPreviewCheckBox.Focus(FocusState.Keyboard);
        }

        private void SignOutButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_signingOut)
            {
                return;
            }

            SignOutConfirmLayer.Visibility = Visibility.Visible;
            ConfirmSignOutButton.IsEnabled = true;
            CancelSignOutButton.IsEnabled = true;
            CancelSignOutButton.Focus(FocusState.Keyboard);
        }

        private async void ConfirmSignOutButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_signingOut)
            {
                return;
            }

            _signingOut = true;
            ConfirmSignOutButton.IsEnabled = false;
            CancelSignOutButton.IsEnabled = false;
            await _sessionStore.ClearAsync();
            AccountBlock.Text = SettingsDiagnosticsFormatter.FormatAccount("", "");
            NavigateLoginAfterSignOut();
        }

        private void CancelSignOutButton_OnClick(object sender, RoutedEventArgs e)
        {
            HideSignOutConfirmation();
        }

        private void HideSignOutConfirmation()
        {
            if (_signingOut)
            {
                return;
            }

            SignOutConfirmLayer.Visibility = Visibility.Collapsed;
            SignOutButton.Focus(FocusState.Keyboard);
        }

        private void NavigateLoginAfterSignOut()
        {
            if (Frame == null || Frame.Content != this)
            {
                return;
            }

            Frame.Navigate(typeof(LoginPage));
            Frame.BackStack.Clear();
        }

        private void ThumbstickSeekPreviewCheckBox_OnChanged(object sender, RoutedEventArgs e)
        {
            if (_loadingSettings)
            {
                return;
            }

            _playbackPreferences.SetThumbstickSeekPreviewEnabled(
                ThumbstickSeekPreviewCheckBox.IsChecked.GetValueOrDefault());
            UpdateThumbstickSeekPreviewStatus();
        }

        private void RegisterSettingsDirectionalFocusHandlers()
        {
            foreach (var control in new Control[] { SignOutButton, ThumbstickSeekPreviewCheckBox })
            {
                control.AddHandler(KeyDownEvent, new KeyEventHandler(SettingsControl_OnKeyDown), handledEventsToo: true);
            }

            foreach (var control in new Control[] { CancelSignOutButton, ConfirmSignOutButton })
            {
                control.AddHandler(KeyDownEvent, new KeyEventHandler(SignOutDialogButton_OnKeyDown), handledEventsToo: true);
            }
        }

        private void SettingsControl_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (IsDownKey(e.Key))
            {
                e.Handled = MoveSettingsFocus(sender, 1);
                return;
            }

            if (IsUpKey(e.Key))
            {
                e.Handled = MoveSettingsFocus(sender, -1);
            }
        }

        private bool MoveSettingsFocus(object sender, int delta)
        {
            var controls = new Control[] { SignOutButton, ThumbstickSeekPreviewCheckBox, DiagnosticsToggleButton };
            var current = sender as Control;
            if (current == null)
            {
                return false;
            }

            var index = Array.IndexOf(controls, current);
            if (index < 0)
            {
                return false;
            }

            var targetIndex = Math.Max(0, Math.Min(controls.Length - 1, index + delta));
            if (targetIndex == index)
            {
                return false;
            }

            return FocusSettingsControl(controls[targetIndex]);
        }

        private void SignOutDialogButton_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (IsBackKey(e.Key))
            {
                HideSignOutConfirmation();
                e.Handled = true;
                return;
            }

            if (IsLeftKey(e.Key))
            {
                e.Handled = FocusSettingsControl(CancelSignOutButton);
                return;
            }

            if (IsRightKey(e.Key))
            {
                e.Handled = FocusSettingsControl(ConfirmSignOutButton);
            }
        }

        private static bool FocusSettingsControl(Control control)
        {
            return control.Focus(FocusState.Keyboard) ||
                control.Focus(FocusState.Programmatic);
        }

        private static bool IsBackKey(VirtualKey key)
        {
            return key == VirtualKey.Escape ||
                key == VirtualKey.GoBack ||
                key == VirtualKey.GamepadB;
        }

        private static bool IsDownKey(VirtualKey key)
        {
            return key == VirtualKey.Down ||
                key == VirtualKey.GamepadDPadDown ||
                key == VirtualKey.GamepadLeftThumbstickDown;
        }

        private static bool IsUpKey(VirtualKey key)
        {
            return key == VirtualKey.Up ||
                key == VirtualKey.GamepadDPadUp ||
                key == VirtualKey.GamepadLeftThumbstickUp;
        }

        private static bool IsLeftKey(VirtualKey key)
        {
            return key == VirtualKey.Left ||
                key == VirtualKey.GamepadDPadLeft ||
                key == VirtualKey.GamepadLeftThumbstickLeft;
        }

        private static bool IsRightKey(VirtualKey key)
        {
            return key == VirtualKey.Right ||
                key == VirtualKey.GamepadDPadRight ||
                key == VirtualKey.GamepadLeftThumbstickRight;
        }

        private void UpdateThumbstickSeekPreviewStatus()
        {
            ThumbstickSeekPreviewStatusBlock.Text = ThumbstickSeekPreviewCheckBox.IsChecked.GetValueOrDefault()
                ? "Left thumbstick previews the target position before seek commits."
                : "Left thumbstick seek preview is off; D-pad seek remains available.";
        }

        private void RenderStartupDiagnostics()
        {
            var lines = ReadStartupDiagnostics();
            var summary = SettingsDiagnosticsFormatter.FormatStartupSummary(lines);
            DiagnosticsSummaryBlock.Text = "Startup: " + summary;
            StartupDiagnosticsBlock.Text = summary;
            StartupDiagnosticsTailBlock.Text = lines.Length == 0
                ? "No log lines recorded."
                : string.Join(Environment.NewLine, lines.Reverse().Take(3).Reverse());
        }

        private void DiagnosticsToggleButton_OnClick(object sender, RoutedEventArgs e)
        {
            _diagnosticsExpanded = !_diagnosticsExpanded;
            UpdateDiagnosticsDisclosure();
        }

        private void UpdateDiagnosticsDisclosure()
        {
            DiagnosticsDetailsPanel.Visibility = _diagnosticsExpanded ? Visibility.Visible : Visibility.Collapsed;
            DiagnosticsToggleButton.Content = _diagnosticsExpanded ? "Hide details" : "Show details";
        }

        private static string[] ReadStartupDiagnostics()
        {
            try
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "startup-diagnostics.log");
                return File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string GetPackageVersion()
        {
            var version = Package.Current.Id.Version;
            return version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;
        }
    }
}
