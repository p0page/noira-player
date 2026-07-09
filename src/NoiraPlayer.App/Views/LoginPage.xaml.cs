using NoiraPlayer.App.ViewModels;
using NoiraPlayer.App.Navigation;
using NoiraPlayer.App.Services;
using NoiraPlayer.Core.Diagnostics;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace NoiraPlayer.App.Views
{
    public sealed partial class LoginPage : Page, ITvContentFocusTarget
    {
#if DEBUG
        private const string DevelopmentLoginFileName = "dev-login.json";
#endif
        private readonly LoginViewModel _viewModel;

        public LoginPage()
        {
            InitializeComponent();
            MatteButtonFocusVisuals.PrepareCommandButton(ConnectButton);
            RegisterLoginDirectionalFocusHandlers();
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
            var connected = false;
            ConnectButton.IsEnabled = false;
            StatusBlock.Text = "Connecting...";

            try
            {
                _viewModel.ServerUrl = ServerUrlBox.Text;
                _viewModel.UserName = UserNameBox.Text;
                _viewModel.Password = PasswordBox.Password;

                connected = await _viewModel.ConnectAsync();
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
                if (!connected)
                {
                    FocusFailedLoginField();
                }
            }
        }

        public bool FocusDefaultContent()
        {
            return ServerUrlBox.Focus(FocusState.Keyboard) ||
                ServerUrlBox.Focus(FocusState.Programmatic);
        }

        private void FocusFailedLoginField()
        {
            var status = StatusBlock.Text ?? "";
            if (status.IndexOf("username", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                UserNameBox.Focus(FocusState.Keyboard);
                return;
            }

            if (status.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                PasswordBox.Focus(FocusState.Keyboard);
                return;
            }

            ServerUrlBox.Focus(FocusState.Keyboard);
        }

        private void LoginControl_OnKeyDown(object sender, KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case VirtualKey.Down:
                    e.Handled = MoveLoginFocus(sender, 1);
                    break;
                case VirtualKey.Up:
                    e.Handled = MoveLoginFocus(sender, -1);
                    break;
            }
        }

        private void RegisterLoginDirectionalFocusHandlers()
        {
            foreach (var control in new Control[] { ServerUrlBox, UserNameBox, PasswordBox, ConnectButton })
            {
                control.AddHandler(KeyDownEvent, new KeyEventHandler(LoginControl_OnKeyDown), handledEventsToo: true);
            }
        }

        private bool MoveLoginFocus(object sender, int delta)
        {
            var controls = new Control[] { ServerUrlBox, UserNameBox, PasswordBox, ConnectButton };
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

            return FocusLoginControl(controls[targetIndex]);
        }

        private static bool FocusLoginControl(Control control)
        {
            return control.Focus(FocusState.Keyboard) ||
                control.Focus(FocusState.Programmatic);
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
            if (Frame == null || !ReferenceEquals(Frame.Content, this))
            {
                PlaybackDiagnosticsLog.WriteLine("LoginPage.NavigateHome skipped because page is no longer active");
                return;
            }

            var rootFrame = Window.Current.Content as Frame;
            var mainPage = rootFrame?.Content as global::NoiraPlayer.App.MainPage;
            if (mainPage != null)
            {
                mainPage.NavigateHome();
                return;
            }

            Frame.Navigate(typeof(HomePage));
        }
    }
}
