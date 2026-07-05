using Windows.ApplicationModel.Activation;
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
            InitializeComponent();
            RequiresPointerMode = ApplicationRequiresPointerMode.WhenRequested;
            UnhandledException += App_OnUnhandledException;
        }

        private void App_OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            PlaybackDiagnosticsLog.WriteLine(
                "UnhandledException " +
                (e.Exception == null ? e.Message : e.Exception.GetType().FullName + " " + e.Exception.Message));
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            ApplicationView.GetForCurrentView().SetDesiredBoundsMode(ApplicationViewBoundsMode.UseCoreWindow);

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
        }
    }
}
