using System.Threading.Tasks;
using NextGenEmby.Core.Emby;
using NextGenEmby.Core.Storage;
using Windows.Security.Credentials;
using Windows.Storage;

namespace NextGenEmby.App.Storage
{
    public sealed class ApplicationDataSessionStore : ISessionStore
    {
        private const string VaultResource = "NextGenEmby.Session";
        private const string ServerUrlKey = "Session.ServerUrl";
        private const string UserIdKey = "Session.UserId";
        private const string UserNameKey = "Session.UserName";

        private readonly ApplicationDataContainer _settings;
        private readonly PasswordVault _vault;

        public ApplicationDataSessionStore()
            : this(ApplicationData.Current.LocalSettings, new PasswordVault())
        {
        }

        public ApplicationDataSessionStore(ApplicationDataContainer settings, PasswordVault vault)
        {
            _settings = settings;
            _vault = vault;
        }

        public Task<EmbySession?> LoadAsync()
        {
            var serverUrl = ReadString(ServerUrlKey);
            var userId = ReadString(UserIdKey);
            var accessToken = ReadAccessToken(userId);

            if (string.IsNullOrWhiteSpace(serverUrl) ||
                string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(accessToken))
            {
                return Task.FromResult<EmbySession?>(null);
            }

            return Task.FromResult<EmbySession?>(new EmbySession
            {
                ServerUrl = serverUrl,
                UserId = userId,
                UserName = ReadString(UserNameKey),
                AccessToken = accessToken
            });
        }

        public Task SaveAsync(EmbySession session)
        {
            _settings.Values[ServerUrlKey] = session.ServerUrl;
            _settings.Values[UserIdKey] = session.UserId;
            _settings.Values[UserNameKey] = session.UserName;
            RemoveAccessTokens();
            _vault.Add(new PasswordCredential(VaultResource, session.UserId, session.AccessToken));
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _settings.Values.Remove(ServerUrlKey);
            _settings.Values.Remove(UserIdKey);
            _settings.Values.Remove(UserNameKey);
            RemoveAccessTokens();
            return Task.CompletedTask;
        }

        private string ReadString(string key)
        {
            object value;
            return _settings.Values.TryGetValue(key, out value) && value != null
                ? value.ToString() ?? ""
                : "";
        }

        private string ReadAccessToken(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return "";
            }

            try
            {
                var credential = _vault.Retrieve(VaultResource, userId);
                credential.RetrievePassword();
                return credential.Password ?? "";
            }
            catch
            {
                return "";
            }
        }

        private void RemoveAccessTokens()
        {
            try
            {
                var credentials = _vault.FindAllByResource(VaultResource);
                foreach (var credential in credentials)
                {
                    _vault.Remove(credential);
                }
            }
            catch
            {
            }
        }
    }
}
