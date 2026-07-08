using Windows.Storage;

namespace NoiraPlayer.App.Storage
{
    public sealed class PlaybackPreferenceStore
    {
        public const string ThumbstickSeekPreviewEnabledKey = "Playback.ThumbstickSeekPreviewEnabled";

        private readonly ApplicationDataContainer _settings;

        public PlaybackPreferenceStore()
            : this(ApplicationData.Current.LocalSettings)
        {
        }

        public PlaybackPreferenceStore(ApplicationDataContainer settings)
        {
            _settings = settings;
        }

        public bool IsThumbstickSeekPreviewEnabled()
        {
            object value;
            if (!_settings.Values.TryGetValue(ThumbstickSeekPreviewEnabledKey, out value) || value == null)
            {
                return true;
            }

            return value is bool enabled ? enabled : true;
        }

        public void SetThumbstickSeekPreviewEnabled(bool isEnabled)
        {
            _settings.Values[ThumbstickSeekPreviewEnabledKey] = isEnabled;
        }
    }
}
