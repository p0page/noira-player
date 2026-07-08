using System;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NoiraPlayer.App.Storage;
using NoiraPlayer.Core.Emby;
using NoiraPlayer.Core.Storage;

namespace NoiraPlayer.App.ViewModels
{
    public sealed class LoginViewModel : INotifyPropertyChanged
    {
        private readonly ISessionStore _sessionStore;
        private readonly Func<EmbyClientOptions, HttpClient> _httpClientFactory;
        private readonly Func<string> _deviceIdProvider;

        private string _serverUrl = "";
        private string _userName = "";
        private string _password = "";
        private string _status = "";
        private bool _isBusy;

        public LoginViewModel()
            : this(new ApplicationDataSessionStore())
        {
        }

        public LoginViewModel(ISessionStore sessionStore)
            : this(
                sessionStore,
                options => new HttpClient(),
                new ApplicationDataDeviceIdProvider().GetOrCreate)
        {
        }

        public LoginViewModel(
            ISessionStore sessionStore,
            Func<EmbyClientOptions, HttpClient> httpClientFactory,
            Func<string> deviceIdProvider)
        {
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _deviceIdProvider = deviceIdProvider ?? throw new ArgumentNullException(nameof(deviceIdProvider));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string ServerUrl
        {
            get { return _serverUrl; }
            set { SetProperty(ref _serverUrl, value ?? ""); }
        }

        public string UserName
        {
            get { return _userName; }
            set { SetProperty(ref _userName, value ?? ""); }
        }

        public string Password
        {
            get { return _password; }
            set { SetProperty(ref _password, value ?? ""); }
        }

        public string Status
        {
            get { return _status; }
            private set { SetProperty(ref _status, value ?? ""); }
        }

        public bool IsBusy
        {
            get { return _isBusy; }
            private set { SetProperty(ref _isBusy, value); }
        }

        public async Task<bool> ConnectAsync()
        {
            if (IsBusy)
            {
                return false;
            }

            var serverUrl = NormalizeServerUrl(ServerUrl);
            if (serverUrl == "")
            {
                Status = "Enter an Emby server URL.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(UserName))
            {
                Status = "Enter your Emby username.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                Status = "Enter your Emby password.";
                return false;
            }

            IsBusy = true;
            Status = "Connecting...";

            try
            {
                var options = CreateOptions(serverUrl);
                using (var http = _httpClientFactory(options))
                {
                    var client = new EmbyApiClient(http, options);
                    var session = await client.AuthenticateAsync(UserName.Trim(), Password);
                    await _sessionStore.SaveAsync(session);
                    Status = string.IsNullOrWhiteSpace(session.UserName)
                        ? "Connected."
                        : "Connected as " + session.UserName + ".";
                    return true;
                }
            }
            catch (UriFormatException)
            {
                Status = "Enter a valid Emby server URL.";
                return false;
            }
            catch (HttpRequestException)
            {
                Status = "Unable to connect. Check the server URL and credentials.";
                return false;
            }
            catch (TaskCanceledException)
            {
                Status = "Connection timed out.";
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Status = ex.Message;
                return false;
            }
            catch (Exception)
            {
                Status = "Login failed. Check the server settings and try again.";
                return false;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private EmbyClientOptions CreateOptions(string serverUrl)
        {
            return new EmbyClientOptions
            {
                ServerUrl = serverUrl,
                ClientName = "Noira",
                ClientVersion = "0.1.0",
                DeviceName = "Xbox",
                DeviceId = _deviceIdProvider()
            };
        }

        private static string NormalizeServerUrl(string serverUrl)
        {
            return string.IsNullOrWhiteSpace(serverUrl)
                ? ""
                : serverUrl.Trim().TrimEnd('/');
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
