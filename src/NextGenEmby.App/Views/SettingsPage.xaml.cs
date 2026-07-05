using NextGenEmby.App.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly ApplicationDataSessionStore _sessionStore = new ApplicationDataSessionStore();

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_OnLoaded;
        }

        private async void SettingsPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingsPage_OnLoaded;
            var session = await _sessionStore.LoadAsync();
            if (session == null)
            {
                AccountBlock.Text = "Not signed in";
                return;
            }

            AccountBlock.Text = string.IsNullOrWhiteSpace(session.UserName)
                ? session.ServerUrl
                : session.UserName + " on " + session.ServerUrl;
        }
    }
}
