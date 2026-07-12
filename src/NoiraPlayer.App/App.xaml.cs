using System;
using System.IO;
using NoiraPlayer.App.Input;
using NoiraPlayer.Core.Input;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NoiraPlayer.App.Services;

namespace NoiraPlayer.App
{
    public sealed partial class App : Application
    {
        private readonly GamepadDeviceRegistry _gamepadDeviceRegistry = new();
#if DEBUG
        private UiThreadResponsivenessWatchdog? _uiResponsivenessWatchdog;
#endif

        public App()
        {
            UnhandledException += App_OnUnhandledException;
            WriteStartupDiagnostic("App.ctor start");

            try
            {
                InitializeComponent();
                WriteStartupDiagnostic("App.InitializeComponent completed");
            }
            catch (Exception ex)
            {
                WriteStartupDiagnostic("App.InitializeComponent exception " + FormatException(ex));
                throw;
            }

            RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
            InputRouter = new GlobalInputRouter(
                _gamepadDeviceRegistry.ResetInputState,
                (context, error) => InputDiagnosticsLog.Write(
                    "input consumer failed context=" + context +
                    " type=" + error.GetType().FullName +
                    " hresult=0x" + error.HResult.ToString("X8")));
            _gamepadDeviceRegistry.Input += InputRouter.Dispatch;
            Suspending += App_OnSuspending;
            Resuming += App_OnResuming;
        }

        internal GlobalInputRouter InputRouter { get; }

        private void App_OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            WriteStartupDiagnostic(
                "UnhandledException " +
                (e.Exception == null ? e.Message : FormatException(e.Exception)));
            PlaybackDiagnosticsLog.WriteLine(
                "UnhandledException " +
                (e.Exception == null ? e.Message : e.Exception.GetType().FullName + " " + e.Exception.Message));
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            try
            {
                WriteStartupDiagnostic("App.OnLaunched start previousState=" + e.PreviousExecutionState);
                ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);
                ApplyA3TitleBarTreatment();

                var rootFrame = Window.Current.Content as Frame;
                if (rootFrame == null)
                {
                    rootFrame = new Frame();
                    Window.Current.Content = rootFrame;
                }

                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                else if (rootFrame.Content is MainPage mainPage)
                {
                    mainPage.NavigateHome();
                }

                Window.Current.Activate();
                _gamepadDeviceRegistry.Start();
#if DEBUG
                if (_uiResponsivenessWatchdog == null)
                {
                    _uiResponsivenessWatchdog = new UiThreadResponsivenessWatchdog(
                        Window.Current.Dispatcher,
                        Path.Combine(ApplicationData.Current.LocalFolder.Path, UiThreadResponsivenessWatchdog.FileName));
                    _uiResponsivenessWatchdog.Start();
                }
#endif
                WriteStartupDiagnostic("App.OnLaunched completed");
            }
            catch (Exception ex)
            {
                WriteStartupDiagnostic("App.OnLaunched exception " + FormatException(ex));
                throw;
            }
        }

        private void App_OnSuspending(object sender, SuspendingEventArgs e)
        {
            _gamepadDeviceRegistry.Stop();
        }

        private void App_OnResuming(object? sender, object e)
        {
            _gamepadDeviceRegistry.Start();
        }

        private static string FormatException(Exception ex)
        {
            return ex.GetType().FullName + " " + ex.Message + Environment.NewLine + ex.StackTrace;
        }

        private static void ApplyA3TitleBarTreatment()
        {
            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            var canvas = Color.FromArgb(255, 5, 7, 10);
            var surface = Color.FromArgb(255, 16, 22, 28);
            var text = Color.FromArgb(255, 238, 243, 246);
            var muted = Color.FromArgb(255, 169, 179, 186);

            titleBar.BackgroundColor = canvas;
            titleBar.ForegroundColor = text;
            titleBar.InactiveBackgroundColor = canvas;
            titleBar.InactiveForegroundColor = muted;
            titleBar.ButtonBackgroundColor = canvas;
            titleBar.ButtonForegroundColor = text;
            titleBar.ButtonHoverBackgroundColor = surface;
            titleBar.ButtonHoverForegroundColor = text;
            titleBar.ButtonPressedBackgroundColor = surface;
            titleBar.ButtonPressedForegroundColor = text;
            titleBar.ButtonInactiveBackgroundColor = canvas;
            titleBar.ButtonInactiveForegroundColor = muted;
        }

        private static void WriteStartupDiagnostic(string message)
        {
            var line = DateTimeOffset.Now.ToString("O") + " " + (message ?? "") + Environment.NewLine;
            TryAppendStartupDiagnostic(Path.Combine(Path.GetTempPath(), "NoiraPlayer-startup-diagnostics.log"), line);

            try
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "startup-diagnostics.log");
                TryAppendStartupDiagnostic(path, line);
            }
            catch
            {
            }
        }

        private static void TryAppendStartupDiagnostic(string path, string line)
        {
            try
            {
                File.AppendAllText(path, line);
            }
            catch
            {
            }
        }
    }
}
