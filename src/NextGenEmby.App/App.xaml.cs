using System;
using System.IO;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using NextGenEmby.App.Services;

namespace NextGenEmby.App
{
    public sealed partial class App : Application
    {
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
        }

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

                Window.Current.Activate();
                WriteStartupDiagnostic("App.OnLaunched completed");
            }
            catch (Exception ex)
            {
                WriteStartupDiagnostic("App.OnLaunched exception " + FormatException(ex));
                throw;
            }
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
            try
            {
                var path = Path.Combine(ApplicationData.Current.LocalFolder.Path, "startup-diagnostics.log");
                File.AppendAllText(
                    path,
                    DateTimeOffset.Now.ToString("O") + " " + (message ?? "") + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
