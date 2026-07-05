using NextGenEmby.App.ViewModels;
using NextGenEmby.App.Services;
using NextGenEmby.Core.Diagnostics;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class LoginPage : Page
    {
#if DEBUG
        private const string DevelopmentLoginFileName = "dev-login.json";
#endif
        private readonly LoginViewModel _viewModel;

        public LoginPage()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            Loaded += LoginPage_OnLoaded;
        }

        private async void LoginPage_OnLoaded(object sender, RoutedEventArgs e)
        {
#if DEBUG
            if (await TryDevelopmentLoginAsync())
            {
                return;
            }
#endif
            ServerUrlBox.Focus(FocusState.Programmatic);
        }

        private async void Connect_OnClick(object sender, RoutedEventArgs e)
        {
            await ConnectAsync();
        }

        private async Task<bool> ConnectAsync()
        {
            ConnectButton.IsEnabled = false;
            StatusBlock.Text = "Connecting...";

            try
            {
                _viewModel.ServerUrl = ServerUrlBox.Text;
                _viewModel.UserName = UserNameBox.Text;
                _viewModel.Password = PasswordBox.Password;

                var connected = await _viewModel.ConnectAsync();
                StatusBlock.Text = _viewModel.Status;
                if (connected)
                {
                    NavigateHome();
                }

                return connected;
            }
            catch
            {
                StatusBlock.Text = "Login failed. Check the server settings and try again.";
                return false;
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }

#if DEBUG
        private async Task<bool> TryDevelopmentLoginAsync()
        {
            try
            {
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(DevelopmentLoginFileName);
                var file = item as StorageFile;
                if (file == null)
                {
                    return false;
                }

                var json = await FileIO.ReadTextAsync(file);
                DevelopmentLoginCredentials? credentials;
                string error;
                if (!DevelopmentLoginCredentials.TryParseJson(json, out credentials, out error) ||
                    credentials == null)
                {
                    StatusBlock.Text = error;
                    return false;
                }

                ServerUrlBox.Text = credentials.ServerUrl;
                UserNameBox.Text = credentials.UserName;
                PasswordBox.Password = credentials.Password;
                StatusBlock.Text = "Using development login...";
                return await ConnectAsync();
            }
            catch (Exception)
            {
                StatusBlock.Text = "Development login config could not be loaded.";
                return false;
            }
        }
#endif

        private void NavigateHome()
        {
            if (Frame == null || Frame.Content != this)
            {
                PlaybackDiagnosticsLog.WriteLine("LoginPage.NavigateHome skipped because page is no longer active");
                return;
            }

            var rootFrame = Window.Current.Content as Frame;
            var mainPage = rootFrame?.Content as global::NextGenEmby.App.MainPage;
            if (mainPage != null)
            {
                mainPage.NavigateHome();
                return;
            }

            Frame.Navigate(typeof(HomePage));
        }
    }
}
