using System;
using System.IO;
using System.Linq;
using NextGenEmby.App.Navigation;
using NextGenEmby.App.Services;
using NextGenEmby.App.Storage;
using NextGenEmby.Core.Diagnostics;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class SettingsPage : Page, ITvContentFocusTarget
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();
        private readonly PlaybackPreferenceStore _playbackPreferences = new PlaybackPreferenceStore();
        private bool _loadingSettings;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_OnLoaded;
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
            _loadingSettings = false;
            FocusDefaultContent();
        }

        public bool FocusDefaultContent()
        {
            return ThumbstickSeekPreviewCheckBox.Focus(FocusState.Keyboard);
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

        private void UpdateThumbstickSeekPreviewStatus()
        {
            ThumbstickSeekPreviewStatusBlock.Text = ThumbstickSeekPreviewCheckBox.IsChecked.GetValueOrDefault()
                ? "Left thumbstick previews the target position before seek commits."
                : "Left thumbstick seek preview is off; D-pad seek remains available.";
        }

        private void RenderStartupDiagnostics()
        {
            var lines = ReadStartupDiagnostics();
            StartupDiagnosticsBlock.Text = SettingsDiagnosticsFormatter.FormatStartupSummary(lines);
            StartupDiagnosticsTailBlock.Text = lines.Length == 0
                ? "No log lines recorded."
                : string.Join(Environment.NewLine, lines.Reverse().Take(3).Reverse());
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
