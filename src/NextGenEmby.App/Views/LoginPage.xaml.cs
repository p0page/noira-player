using NextGenEmby.App.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NextGenEmby.App.Views
{
    public sealed partial class LoginPage : Page
    {
        private readonly LoginViewModel _viewModel;

        public LoginPage()
        {
            InitializeComponent();
            _viewModel = new LoginViewModel();
            Loaded += LoginPage_OnLoaded;
        }

        private void LoginPage_OnLoaded(object sender, RoutedEventArgs e)
        {
            ServerUrlBox.Focus(FocusState.Programmatic);
        }

        private async void Connect_OnClick(object sender, RoutedEventArgs e)
        {
            ConnectButton.IsEnabled = false;
            StatusBlock.Text = "Connecting...";

            try
            {
                _viewModel.ServerUrl = ServerUrlBox.Text;
                _viewModel.UserName = UserNameBox.Text;
                _viewModel.Password = PasswordBox.Password;

                await _viewModel.ConnectAsync();
                StatusBlock.Text = _viewModel.Status;
            }
            catch
            {
                StatusBlock.Text = "Login failed. Check the server settings and try again.";
            }
            finally
            {
                ConnectButton.IsEnabled = true;
            }
        }
    }
}
